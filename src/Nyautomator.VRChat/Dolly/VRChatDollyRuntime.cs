using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Coordinates VRChat dolly configuration, track storage, capture, playback, and event emission.
/// </summary>
public static class VRChatDollyRuntime
{
    /// <summary>
    /// Synchronizes mutable runtime configuration and capture trigger state.
    /// </summary>
    private static readonly object ConfigureSync = new();

    /// <summary>
    /// Current normalized dolly options.
    /// </summary>
    private static VRChatDollyOptions _options = VRChatDollyOptions.Default();

    /// <summary>
    /// Track store for the currently configured track directory.
    /// </summary>
    private static VRChatDollyTrackStore _store = new(_options.TrackDirectory);

    /// <summary>
    /// Playback service bound to the current track store.
    /// </summary>
    private static VRChatDollyPlaybackService _playback = new(_store);

    /// <summary>
    /// Track selected for capture operations triggered by avatar parameters.
    /// </summary>
    private static string? _activeCaptureTrackId;

    /// <summary>
    /// Tracks whether the avatar parameter event has been wired into the runtime.
    /// </summary>
    private static bool _avatarTriggerWired;

    /// <summary>
    /// Tracks the pressed state of the configured avatar capture parameter for edge-triggered capture.
    /// </summary>
    private static bool _avatarCaptureTriggerPressed;

    /// <summary>
    /// Prevents overlapping asynchronous captures from repeated avatar parameter updates.
    /// </summary>
    private static bool _avatarCaptureInFlight;

    /// <summary>
    /// Raised whenever the dolly runtime emits a track, capture, or playback event.
    /// </summary>
    public static event Action<VRChatDollyEvent>? EventEmitted;

    /// <summary>
    /// Applies module configuration to dolly options, OSC freshness windows, stores, and event wiring.
    /// </summary>
    /// <param name="optionsInput">Module-owned Dolly options.</param>
    /// <param name="defaultTrackDirectory">Default track directory supplied by the module host.</param>
    public static void Configure(VRChatDollyOptionsInput? optionsInput, string? defaultTrackDirectory = null)
    {
        var options = VRChatDollyOptions.FromConfiguration(optionsInput, defaultTrackDirectory);

        lock (ConfigureSync)
        {
            _options = options;
            VRChatHelper.OSC.CameraPoseFreshness = TimeSpan.FromMilliseconds(Math.Max(250, options.PoseFreshnessMilliseconds));
            VRChatHelper.OSC.CameraSettingsFreshness = TimeSpan.FromMilliseconds(Math.Max(250, options.SettingsFreshnessMilliseconds));

            if (!_avatarTriggerWired)
            {
                VRChatHelper.OSC.OnAvatarParameterChanged += OnAvatarParameterChanged;
                _avatarTriggerWired = true;
            }

            if (!string.Equals(_store.TrackDirectory, options.TrackDirectory, StringComparison.OrdinalIgnoreCase))
            {
                _store = new VRChatDollyTrackStore(options.TrackDirectory);
                _playback = new VRChatDollyPlaybackService(_store);
                _activeCaptureTrackId = null;
            }
        }
    }

    /// <summary>
    /// Builds a status snapshot from current options, OSC camera state, write confirmations, and active playbacks.
    /// </summary>
    /// <returns>Current dolly runtime status.</returns>
    public static VRChatDollyStatus GetStatus()
    {
        var options = _options;
        var camera = VRChatHelper.OSC.CurrentCameraSnapshot;
        var lastPoseWrite = VRChatHelper.OSC.LastUserCameraPoseWriteUtc;
        var lastSettingsWrite = VRChatHelper.OSC.LastUserCameraSettingsWriteUtc;

        var poseWriteConfirmed = lastPoseWrite.HasValue
            && camera.LastPoseReceivedUtc.HasValue
            && camera.LastPoseReceivedUtc.Value >= lastPoseWrite.Value.AddMilliseconds(-250);

        var settingsWriteConfirmed = lastSettingsWrite.HasValue
            && camera.LastSettingsReceivedUtc.HasValue
            && camera.LastSettingsReceivedUtc.Value >= lastSettingsWrite.Value.AddMilliseconds(-250);

        return new VRChatDollyStatus(
            options.Enabled,
            VRChatHelper.OSC.Sending,
            VRChatHelper.OSC.Listening,
            camera.LastPoseReceivedUtc.HasValue,
            poseWriteConfirmed,
            camera.LastSettingsReceivedUtc.HasValue,
            settingsWriteConfirmed,
            camera.LastPoseReceivedUtc,
            camera.LastSettingsReceivedUtc,
            lastPoseWrite,
            lastSettingsWrite,
            _activeCaptureTrackId,
            _store.TrackDirectory,
            _playback.GetStates(),
            camera);
    }

    /// <summary>
    /// Lists persisted tracks from the configured track directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for file reads.</param>
    /// <returns>Tracks ordered by most recent update.</returns>
    public static Task<IReadOnlyList<VRChatDollyTrack>> ListTracksAsync(CancellationToken cancellationToken = default)
        => _store.ListTracksAsync(cancellationToken);

    /// <summary>
    /// Loads a single dolly track by identifier.
    /// </summary>
    /// <param name="trackId">Track identifier to load.</param>
    /// <param name="cancellationToken">Cancellation token for the file read.</param>
    /// <returns>The track when found; otherwise <see langword="null"/>.</returns>
    public static Task<VRChatDollyTrack?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
        => _store.GetTrackAsync(trackId, cancellationToken);

    /// <summary>
    /// Creates, saves, and selects a new dolly track.
    /// </summary>
    /// <param name="name">Optional display name for the new track.</param>
    /// <param name="cancellationToken">Cancellation token for the save operation.</param>
    /// <returns>The saved track.</returns>
    public static async Task<VRChatDollyTrack> CreateTrackAsync(string? name, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var track = new VRChatDollyTrack
        {
            Id = VRChatDollyKeyframe.CreateId("track"),
            Name = string.IsNullOrWhiteSpace(name) ? "New Dolly Track" : name.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _store.SaveTrackAsync(track, cancellationToken).ConfigureAwait(false);
        SetActiveTrackId(track.Id);
        Emit("trackCreated", track.Id, $"Created VRChat dolly track '{track.Name}'.", new { track.Id, track.Name });
        return track;
    }

    /// <summary>
    /// Saves a track, selects it for capture, and emits a track-saved event.
    /// </summary>
    /// <param name="track">Track to normalize and persist.</param>
    /// <param name="cancellationToken">Cancellation token for the save operation.</param>
    /// <returns>The normalized saved track.</returns>
    public static async Task<VRChatDollyTrack> SaveTrackAsync(VRChatDollyTrack track, CancellationToken cancellationToken = default)
    {
        var saved = await _store.SaveTrackAsync(track, cancellationToken).ConfigureAwait(false);
        SetActiveTrackId(saved.Id);
        Emit("trackSaved", saved.Id, $"Saved VRChat dolly track '{saved.Name}'.", new { saved.Id, saved.Name });
        return saved;
    }

    /// <summary>
    /// Sets the active capture track after confirming it exists in the store.
    /// </summary>
    /// <param name="trackId">Track identifier to select.</param>
    /// <param name="cancellationToken">Cancellation token for the lookup.</param>
    /// <returns>An operation result containing the selected track or a failure message.</returns>
    public static async Task<VRChatDollyOperationResult> SetActiveTrackAsync(
        string trackId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            return Failure("TrackId is required.");

        var track = await _store.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (track is null)
            return Failure("Track not found.");

        SetActiveTrackId(track.Id);
        Emit("activeTrackChanged", track.Id, $"Set active VRChat dolly capture track to '{track.Name}'.", new { track.Id, track.Name });
        return new VRChatDollyOperationResult(true, "Active capture track updated.", Track: track);
    }

    /// <summary>
    /// Stops playback for a track, deletes its persisted file, and emits a deletion event when successful.
    /// </summary>
    /// <param name="trackId">Track identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token for stop and delete operations.</param>
    /// <returns><see langword="true"/> when a track file was deleted.</returns>
    public static async Task<bool> DeleteTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        await _playback.StopTrackAsync(trackId, null, cancellationToken).ConfigureAwait(false);
        var deleted = await _store.DeleteTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (deleted)
            Emit("trackDeleted", trackId, "Deleted VRChat dolly track.", new { trackId });
        return deleted;
    }

    /// <summary>
    /// Creates a copy of an existing track with new track and keyframe identifiers.
    /// </summary>
    /// <param name="trackId">Source track identifier.</param>
    /// <param name="cancellationToken">Cancellation token for load and save operations.</param>
    /// <returns>The duplicated track, or <see langword="null"/> when the source is missing.</returns>
    public static async Task<VRChatDollyTrack?> DuplicateTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var copy = await _store.DuplicateTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (copy is not null)
        {
            SetActiveTrackId(copy.Id);
            Emit("trackDuplicated", copy.Id, $"Duplicated VRChat dolly track '{copy.Name}'.", new { copy.Id, copy.Name, sourceTrackId = trackId });
        }
        return copy;
    }

    /// <summary>
    /// Captures the latest VRChat user camera snapshot into a track according to the requested capture mode.
    /// </summary>
    /// <param name="request">Capture request that identifies the track and keyframe behavior.</param>
    /// <param name="cancellationToken">Cancellation token for track load and save operations.</param>
    /// <returns>An operation result containing the saved track and captured keyframe when successful.</returns>
    public static async Task<VRChatDollyOperationResult> CaptureKeyframeAsync(
        VRChatDollyCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Failure("VRChat Dolly is disabled.");

        if (string.IsNullOrWhiteSpace(request.TrackId))
            return Failure("TrackId is required.");

        var track = await _store.GetTrackAsync(request.TrackId, cancellationToken).ConfigureAwait(false);
        if (track is null)
            return Failure("Track not found.");

        var snapshot = VRChatHelper.OSC.CurrentCameraSnapshot;
        if (request.IncludePose && (snapshot.Pose is null || !snapshot.PoseFresh))
            return Failure("No fresh VRChat camera pose is available. Open the VRChat camera and ensure OSC listening is running.");

        if (request.IncludeSettings && !snapshot.SettingsFresh)
        {
            // Settings are useful but not strictly required; keep the capture moving.
            Emit("captureWarning", track.Id, "Camera settings are stale; captured available settings only.");
        }

        var keyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).Select(k => k.Clone()).ToList();
        VRChatDollyKeyframe keyframe;

        switch (request.Mode)
        {
            case VRChatDollyCaptureMode.ReplaceSelected:
            case VRChatDollyCaptureMode.UpdatePoseOnly:
            case VRChatDollyCaptureMode.UpdateSettingsOnly:
                keyframe = FindKeyframe(keyframes, request.KeyframeId, request.TimeSeconds)
                    ?? CreateCaptureKeyframe(snapshot, request.TimeSeconds ?? NextAppendTime(keyframes), request.IncludePose, request.IncludeSettings);
                ApplySnapshotToKeyframe(keyframe, snapshot, request.IncludePose, request.IncludeSettings);
                if (!keyframes.Any(k => string.Equals(k.Id, keyframe.Id, StringComparison.OrdinalIgnoreCase)))
                    keyframes.Add(keyframe);
                break;
            case VRChatDollyCaptureMode.InsertAtTime:
                keyframe = CreateCaptureKeyframe(snapshot, request.TimeSeconds ?? NextAppendTime(keyframes), request.IncludePose, request.IncludeSettings);
                keyframes.Add(keyframe);
                break;
            default:
                keyframe = CreateCaptureKeyframe(snapshot, NextAppendTime(keyframes), request.IncludePose, request.IncludeSettings);
                keyframes.Add(keyframe);
                break;
        }

        track.Keyframes = keyframes.OrderBy(k => k.TimeSeconds).ToList();
        track.DurationSeconds = Math.Max(track.DurationSeconds, track.Keyframes.Count == 0 ? 0d : track.Keyframes.Max(k => k.TimeSeconds));
        track.UpdatedAtUtc = DateTime.UtcNow;

        var saved = await _store.SaveTrackAsync(track, cancellationToken).ConfigureAwait(false);
        SetActiveTrackId(saved.Id);
        Emit("keyframeCaptured", saved.Id, $"Captured keyframe at {keyframe.TimeSeconds:0.###}s.", new { keyframe.Id, keyframe.TimeSeconds });

        return new VRChatDollyOperationResult(true, "Keyframe captured.", Track: saved, Keyframe: keyframe);
    }

    /// <summary>
    /// Starts playback of a stored dolly track through the playback service.
    /// </summary>
    /// <param name="request">Playback request describing track, loops, frame rate, and settings mode.</param>
    /// <param name="cancellationToken">Cancellation token for startup validation.</param>
    /// <returns>An operation result describing whether playback started.</returns>
    public static Task<VRChatDollyOperationResult> PlayTrackAsync(
        VRChatDollyPlaybackRequest request,
        CancellationToken cancellationToken = default)
        => _playback.PlayAsync(request, cancellationToken);

    /// <summary>
    /// Stops active playback sessions matching an optional track and stop group.
    /// </summary>
    /// <param name="trackId">Optional track identifier filter.</param>
    /// <param name="stopGroup">Optional stop group filter.</param>
    /// <param name="cancellationToken">Cancellation token checked before cancellation is requested.</param>
    /// <returns>An operation result reporting how many sessions were stopped.</returns>
    public static async Task<VRChatDollyOperationResult> StopTrackAsync(
        string? trackId,
        string? stopGroup,
        CancellationToken cancellationToken = default)
    {
        var stopped = await _playback.StopTrackAsync(trackId, stopGroup, cancellationToken).ConfigureAwait(false);
        return stopped > 0
            ? new VRChatDollyOperationResult(true, $"Stopped {stopped} VRChat dolly playback session(s).")
            : new VRChatDollyOperationResult(true, "No matching VRChat dolly playback sessions were running.");
    }

    /// <summary>
    /// Emits a dolly event to subscribers while shielding the runtime from subscriber exceptions.
    /// </summary>
    /// <param name="type">Machine-readable event type.</param>
    /// <param name="trackId">Related track identifier when available.</param>
    /// <param name="message">Human-readable event message.</param>
    /// <param name="payload">Optional structured payload.</param>
    internal static void Emit(string type, string? trackId, string message, object? payload = null)
    {
        var evt = new VRChatDollyEvent(type, DateTime.UtcNow, trackId, message, payload);
        try { EventEmitted?.Invoke(evt); } catch { }
    }

    /// <summary>
    /// Stores the active capture track identifier in normalized nullable form.
    /// </summary>
    /// <param name="trackId">Track identifier to store, or blank to clear.</param>
    private static void SetActiveTrackId(string? trackId)
    {
        lock (ConfigureSync)
        {
            _activeCaptureTrackId = string.IsNullOrWhiteSpace(trackId) ? null : trackId.Trim();
        }
    }

    /// <summary>
    /// Watches the configured avatar parameter for a rising pressed state and starts capture for the active track.
    /// </summary>
    /// <param name="payload">Avatar parameter change payload from the OSC helper.</param>
    private static void OnAvatarParameterChanged(VRChatAvatarParameterChangedEvent payload)
    {
        var options = _options;
        if (!options.Enabled || !options.EnableAvatarCaptureTrigger)
            return;

        if (!IsCaptureAvatarParameter(payload, options.CaptureAvatarParameter))
            return;

        var pressed = payload.BoolValue || Math.Abs(payload.FloatValue) >= 0.5f || payload.IntValue != 0;
        string? trackId;

        lock (ConfigureSync)
        {
            if (!pressed)
            {
                _avatarCaptureTriggerPressed = false;
                return;
            }

            if (_avatarCaptureTriggerPressed || _avatarCaptureInFlight)
                return;

            _avatarCaptureTriggerPressed = true;
            trackId = _activeCaptureTrackId;
            if (!string.IsNullOrWhiteSpace(trackId))
                _avatarCaptureInFlight = true;
        }

        if (string.IsNullOrWhiteSpace(trackId))
        {
            Emit("captureFailed", null, "VRChat dolly capture trigger fired, but no active track is selected.");
            return;
        }

        _ = Task.Run(() => CaptureFromAvatarTriggerAsync(trackId));
    }

    /// <summary>
    /// Checks whether an avatar parameter change matches the configured capture trigger name or address.
    /// </summary>
    /// <param name="payload">Avatar parameter change to inspect.</param>
    /// <param name="configuredParameter">Configured parameter name or /avatar/parameters/ address.</param>
    /// <returns><see langword="true"/> when the payload should trigger capture.</returns>
    private static bool IsCaptureAvatarParameter(VRChatAvatarParameterChangedEvent payload, string configuredParameter)
    {
        if (string.IsNullOrWhiteSpace(configuredParameter))
            return false;

        var name = configuredParameter.Trim();
        if (name.StartsWith("/avatar/parameters/", StringComparison.OrdinalIgnoreCase))
            name = name["/avatar/parameters/".Length..];

        return string.Equals(payload.ParameterName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(payload.Address, $"/avatar/parameters/{name}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs avatar-triggered capture asynchronously and reports capture failures as dolly events.
    /// </summary>
    /// <param name="trackId">Active track identifier to capture into.</param>
    /// <returns>A task that completes after the capture attempt finishes.</returns>
    private static async Task CaptureFromAvatarTriggerAsync(string trackId)
    {
        try
        {
            var result = await CaptureKeyframeAsync(new VRChatDollyCaptureRequest(trackId)).ConfigureAwait(false);
            if (!result.Success)
                Emit("captureFailed", trackId, result.Message);
        }
        catch (Exception ex)
        {
            Emit("captureFailed", trackId, $"VRChat dolly capture trigger failed: {ex.Message}");
        }
        finally
        {
            lock (ConfigureSync)
            {
                _avatarCaptureInFlight = false;
            }
        }
    }

    /// <summary>
    /// Creates a failed dolly operation result with matching message and error text.
    /// </summary>
    /// <param name="error">Failure message.</param>
    /// <returns>A failed operation result.</returns>
    private static VRChatDollyOperationResult Failure(string error)
        => new(false, error, error);

    /// <summary>
    /// Finds a keyframe by identifier first, then by near-exact track time.
    /// </summary>
    /// <param name="keyframes">Candidate keyframes.</param>
    /// <param name="keyframeId">Optional identifier to match.</param>
    /// <param name="timeSeconds">Optional track time to match within a small tolerance.</param>
    /// <returns>The matching keyframe, or <see langword="null"/>.</returns>
    private static VRChatDollyKeyframe? FindKeyframe(List<VRChatDollyKeyframe> keyframes, string? keyframeId, double? timeSeconds)
    {
        if (!string.IsNullOrWhiteSpace(keyframeId))
        {
            var byId = keyframes.FirstOrDefault(k => string.Equals(k.Id, keyframeId, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
                return byId;
        }

        if (timeSeconds.HasValue)
            return keyframes.FirstOrDefault(k => Math.Abs(k.TimeSeconds - timeSeconds.Value) < 0.001d);

        return null;
    }

    /// <summary>
    /// Chooses the append time for a new keyframe, using zero for an empty track or two seconds after the last keyframe.
    /// </summary>
    /// <param name="keyframes">Existing keyframes.</param>
    /// <returns>Track time for the appended keyframe.</returns>
    private static double NextAppendTime(List<VRChatDollyKeyframe> keyframes)
    {
        if (keyframes.Count == 0)
            return 0d;

        return keyframes.Max(k => k.TimeSeconds) + 2d;
    }

    /// <summary>
    /// Creates a new keyframe and applies the requested parts of a camera snapshot to it.
    /// </summary>
    /// <param name="snapshot">Camera snapshot to copy from.</param>
    /// <param name="timeSeconds">Requested keyframe time.</param>
    /// <param name="includePose">Whether pose values should be copied.</param>
    /// <param name="includeSettings">Whether camera settings should be copied.</param>
    /// <returns>A new keyframe with a generated identifier.</returns>
    private static VRChatDollyKeyframe CreateCaptureKeyframe(
        VRChatCameraSnapshot snapshot,
        double timeSeconds,
        bool includePose,
        bool includeSettings)
    {
        var keyframe = new VRChatDollyKeyframe
        {
            Id = VRChatDollyKeyframe.CreateId("kf"),
            TimeSeconds = Math.Max(0d, timeSeconds)
        };

        ApplySnapshotToKeyframe(keyframe, snapshot, includePose, includeSettings);
        return keyframe;
    }

    /// <summary>
    /// Copies pose and/or camera settings from a snapshot into an existing keyframe.
    /// </summary>
    /// <param name="keyframe">Keyframe to update.</param>
    /// <param name="snapshot">Camera snapshot to copy from.</param>
    /// <param name="includePose">Whether pose values should be copied.</param>
    /// <param name="includeSettings">Whether camera settings should be copied.</param>
    private static void ApplySnapshotToKeyframe(
        VRChatDollyKeyframe keyframe,
        VRChatCameraSnapshot snapshot,
        bool includePose,
        bool includeSettings)
    {
        if (includePose && snapshot.Pose is not null)
        {
            keyframe.Position = VRChatDollyVector3.From(snapshot.Pose.Position);
            keyframe.EulerDegrees = VRChatDollyVector3.From(snapshot.Pose.EulerDegrees);
            keyframe.Rotation = VRChatDollyQuaternion.From(snapshot.Pose.Rotation);
        }

        if (includeSettings)
        {
            keyframe.Camera = new VRChatDollyCameraSettings
            {
                Mode = snapshot.Mode,
                ModeName = snapshot.ModeName,
                Sliders = new Dictionary<string, float>(snapshot.Sliders, StringComparer.OrdinalIgnoreCase),
                Toggles = new Dictionary<string, bool>(snapshot.Toggles, StringComparer.OrdinalIgnoreCase)
            };
        }
    }
}
