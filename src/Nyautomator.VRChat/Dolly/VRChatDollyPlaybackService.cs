using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Runs active VRChat dolly playback sessions and sends sampled frames over OSC.
/// </summary>
/// <remarks>
/// Creates a playback service that reads tracks from the supplied store.
/// </remarks>
/// <param name="store">Track store used for playback validation and loading.</param>
public sealed partial class VRChatDollyPlaybackService(VRChatDollyTrackStore store)
{
    /// <summary>
    /// Track store used to load playback targets.
    /// </summary>
    private readonly VRChatDollyTrackStore _store = store;

    /// <summary>
    /// Active playback sessions keyed by playback identifier.
    /// </summary>
    private readonly ConcurrentDictionary<string, ActivePlayback> _active = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets snapshots of all currently active playback sessions ordered by start time.
    /// </summary>
    /// <returns>Active playback states.</returns>
    public IReadOnlyList<VRChatDollyPlaybackState> GetStates()
        => _active.Values.Select(p => p.GetState()).OrderBy(p => p.StartedAtUtc).ToList();

    /// <summary>
    /// Validates a playback request, stops any existing playback for the same track, and starts a new session.
    /// </summary>
    /// <param name="request">Playback request to start.</param>
    /// <param name="cancellationToken">Cancellation token for startup validation.</param>
    /// <returns>An operation result containing the started playback state or a validation failure.</returns>
    public async Task<VRChatDollyOperationResult> PlayAsync(
        VRChatDollyPlaybackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TrackId))
            return new VRChatDollyOperationResult(false, "TrackId is required.", "TrackId is required.");

        var track = await _store.GetTrackAsync(request.TrackId, cancellationToken).ConfigureAwait(false);
        if (track is null)
            return new VRChatDollyOperationResult(false, "Track not found.", "Track not found.");

        if (!VRChatHelper.OSC.Sending)
            return new VRChatDollyOperationResult(false, "VRChat OSC sending is not active.", "VRChat OSC sending is not active.");

        if (track.Keyframes.Count == 0)
            return new VRChatDollyOperationResult(false, "Track has no keyframes.", "Track has no keyframes.", Track: track);

        await StopTrackAsync(track.Id, null, cancellationToken).ConfigureAwait(false);

        var normalized = NormalizeRequest(request);
        var playback = new ActivePlayback(track, normalized);
        _active[playback.PlaybackId] = playback;

        playback.RunTask = Task.Run(() => RunPlaybackAsync(playback), CancellationToken.None);
        VRChatDollyRuntime.Emit("playbackStarted", track.Id, $"Started VRChat dolly track '{track.Name}'.", playback.GetState());
        return new VRChatDollyOperationResult(true, "Playback started.", Track: track, Playback: playback.GetState());
    }

    /// <summary>
    /// Cancels active playback sessions that match the optional track and stop group filters.
    /// </summary>
    /// <param name="trackId">Optional track identifier filter.</param>
    /// <param name="stopGroup">Optional stop group filter.</param>
    /// <param name="cancellationToken">Cancellation token checked before cancellation starts.</param>
    /// <returns>The number of sessions that were asked to stop.</returns>
    public Task<int> StopTrackAsync(string? trackId, string? stopGroup, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopped = 0;

        foreach (var playback in _active.Values.ToList())
        {
            if (!string.IsNullOrWhiteSpace(trackId)
                && !string.Equals(playback.Track.Id, trackId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(stopGroup)
                && !string.Equals(playback.Request.StopGroup, stopGroup, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            playback.Stop("Stopped by request.");
            stopped++;
        }

        return Task.FromResult(stopped);
    }

    /// <summary>
    /// Clamps playback frame rate, loop count, and start delay into runtime-safe values.
    /// </summary>
    /// <param name="request">Playback request to normalize.</param>
    /// <returns>A normalized playback request.</returns>
    private static VRChatDollyPlaybackRequest NormalizeRequest(VRChatDollyPlaybackRequest request)
    {
        var frameRate = Math.Clamp(request.FrameRate <= 0 ? 60 : request.FrameRate, 1, 120);
        var repeatCount = request.RunMode == VRChatDollyRunMode.Count
            ? Math.Max(1, request.RepeatCount)
            : request.RunMode == VRChatDollyRunMode.Once
                ? 1
                : Math.Max(1, request.RepeatCount);

        return request with
        {
            FrameRate = frameRate,
            RepeatCount = repeatCount,
            StartDelay = request.StartDelay.HasValue && request.StartDelay.Value > TimeSpan.Zero
                ? request.StartDelay.Value
                : TimeSpan.Zero
        };
    }

    /// <summary>
    /// Executes a playback session loop, sampling frames, applying settings, emitting progress, and handling cancellation.
    /// </summary>
    /// <param name="playback">Active playback session to run.</param>
    /// <returns>A task that completes when playback finishes, fails, or is cancelled.</returns>
    private async Task RunPlaybackAsync(ActivePlayback playback)
    {
        var token = playback.Cts.Token;

        try
        {
            if (playback.Request.StartDelay is { } delay && delay > TimeSpan.Zero)
                await Task.Delay(delay, token).ConfigureAwait(false);

            var keyframes = playback.Track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
            var durationSeconds = GetDurationSeconds(playback.Track, keyframes);
            var frameDelay = TimeSpan.FromSeconds(1d / playback.Request.FrameRate);
            var loopsToRun = playback.Request.RunMode switch
            {
                VRChatDollyRunMode.Once => 1,
                VRChatDollyRunMode.Count => playback.Request.RepeatCount,
                _ => int.MaxValue
            };

            while (!token.IsCancellationRequested && playback.CompletedLoops < loopsToRun)
            {
                var loopIndex = playback.CompletedLoops;
                var appliedKeyframeIndex = -1;
                var stopwatch = Stopwatch.StartNew();
                DateTime lastFrameEventUtc = DateTime.MinValue;

                ApplyTrackStartSettings(playback, keyframes);

                if (durationSeconds <= 0.0001d)
                {
                    var frame = VRChatDollyInterpolator.Sample(playback.Track, keyframes, 0d);
                    SendFrame(frame, playback.Request.SettingsApplyMode, applySettings: playback.Request.SettingsApplyMode != VRChatDollySettingsApplyMode.PoseOnly);
                    playback.TrackTimeSeconds = 0d;
                    playback.CompletedLoops++;
                    VRChatDollyRuntime.Emit("loopCompleted", playback.Track.Id, "Completed VRChat dolly loop.", new { playback.Track.Id, loopIndex });
                    break;
                }

                while (!token.IsCancellationRequested)
                {
                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    if (elapsedSeconds > durationSeconds)
                        break;

                    var frame = VRChatDollyInterpolator.Sample(playback.Track, keyframes, elapsedSeconds);
                    playback.TrackTimeSeconds = elapsedSeconds;

                    SendFrame(
                        frame,
                        playback.Request.SettingsApplyMode,
                        applySettings: playback.Request.SettingsApplyMode == VRChatDollySettingsApplyMode.EveryFrame);

                    if (playback.Request.SettingsApplyMode == VRChatDollySettingsApplyMode.AtKeyframes)
                        appliedKeyframeIndex = ApplyCrossedKeyframeSettings(keyframes, elapsedSeconds, appliedKeyframeIndex);

                    var now = DateTime.UtcNow;
                    if (now - lastFrameEventUtc >= TimeSpan.FromSeconds(1))
                    {
                        lastFrameEventUtc = now;
                        VRChatDollyRuntime.Emit("frameSent", playback.Track.Id, "Sent VRChat dolly frame.", new
                        {
                            playback.Track.Id,
                            loopIndex,
                            trackTimeSeconds = elapsedSeconds
                        });
                    }

                    await Task.Delay(frameDelay, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested)
                    break;

                var finalFrame = VRChatDollyInterpolator.Sample(playback.Track, keyframes, durationSeconds);
                SendFrame(
                    finalFrame,
                    playback.Request.SettingsApplyMode,
                    applySettings: playback.Request.SettingsApplyMode == VRChatDollySettingsApplyMode.EveryFrame);

                if (playback.Request.SettingsApplyMode == VRChatDollySettingsApplyMode.AtKeyframes)
                    ApplyCrossedKeyframeSettings(keyframes, durationSeconds, appliedKeyframeIndex);

                playback.TrackTimeSeconds = durationSeconds;
                playback.CompletedLoops++;
                VRChatDollyRuntime.Emit("loopCompleted", playback.Track.Id, "Completed VRChat dolly loop.", new
                {
                    playback.Track.Id,
                    loopIndex = playback.CompletedLoops
                });
            }

            playback.MarkStopped(null);
            VRChatDollyRuntime.Emit("playbackStopped", playback.Track.Id, $"Stopped VRChat dolly track '{playback.Track.Name}'.", playback.GetState());
        }
        catch (OperationCanceledException)
        {
            playback.MarkStopped(null);
            VRChatDollyRuntime.Emit("playbackStopped", playback.Track.Id, $"Stopped VRChat dolly track '{playback.Track.Name}'.", playback.GetState());
        }
        catch (Exception ex)
        {
            playback.MarkStopped(ex.Message);
            VRChatDollyRuntime.Emit("playbackFailed", playback.Track.Id, $"VRChat dolly playback failed: {ex.Message}", playback.GetState());
        }
        finally
        {
            _active.TryRemove(playback.PlaybackId, out _);
            playback.Dispose();
        }
    }

    /// <summary>
    /// Calculates playback duration from the track duration and the latest keyframe time.
    /// </summary>
    /// <param name="track">Track whose configured duration is considered.</param>
    /// <param name="keyframes">Ordered keyframes to inspect.</param>
    /// <returns>Duration in seconds.</returns>
    private static double GetDurationSeconds(VRChatDollyTrack track, IReadOnlyList<VRChatDollyKeyframe> keyframes)
    {
        var lastKeyframe = keyframes.Count == 0 ? 0d : keyframes.Max(k => k.TimeSeconds);
        return Math.Max(track.DurationSeconds, lastKeyframe);
    }

    /// <summary>
    /// Applies the first keyframe's settings at the beginning of a loop when the settings mode requires it.
    /// </summary>
    /// <param name="playback">Playback session whose settings mode is inspected.</param>
    /// <param name="keyframes">Ordered keyframes for the track.</param>
    private static void ApplyTrackStartSettings(ActivePlayback playback, IReadOnlyList<VRChatDollyKeyframe> keyframes)
    {
        if ((playback.Request.SettingsApplyMode is VRChatDollySettingsApplyMode.PoseOnly or VRChatDollySettingsApplyMode.EveryFrame) || keyframes.Count == 0)
            return;

        var first = keyframes[0];
        if (first is not null)
            ApplySettings(first.Camera);
    }

    /// <summary>
    /// Applies settings for keyframes whose timestamps have been crossed since the previous frame.
    /// </summary>
    /// <param name="keyframes">Ordered keyframes for the track.</param>
    /// <param name="elapsedSeconds">Current playback time in seconds.</param>
    /// <param name="appliedKeyframeIndex">Index of the most recent keyframe whose settings were already applied.</param>
    /// <returns>The latest keyframe index whose settings have been applied.</returns>
    private static int ApplyCrossedKeyframeSettings(
        IReadOnlyList<VRChatDollyKeyframe> keyframes,
        double elapsedSeconds,
        int appliedKeyframeIndex)
    {
        for (var i = appliedKeyframeIndex + 1; i < keyframes.Count; i++)
        {
            if (keyframes[i].TimeSeconds > elapsedSeconds + 0.0001d)
                break;

            ApplySettings(keyframes[i].Camera);
            appliedKeyframeIndex = i;
        }

        return appliedKeyframeIndex;
    }

    /// <summary>
    /// Sends one sampled frame's pose and, when requested, camera settings to VRChat.
    /// </summary>
    /// <param name="frame">Sampled frame to send.</param>
    /// <param name="settingsMode">Settings mode selected for playback.</param>
    /// <param name="applySettings">Whether this frame should send settings.</param>
    private static void SendFrame(VRChatDollyFrame frame, VRChatDollySettingsApplyMode settingsMode, bool applySettings)
    {
        VRChatHelper.OSC.SetCameraPose(frame.Position, frame.EulerDegrees);

        if (!applySettings || settingsMode == VRChatDollySettingsApplyMode.PoseOnly)
            return;
        
        if (frame.Mode.HasValue)
            VRChatHelper.OSC.SetCameraMode(frame.Mode.Value);

        foreach (var toggle in frame.Toggles)
            VRChatHelper.OSC.SetCameraToggle(toggle.Key, toggle.Value);

        foreach (var slider in frame.Sliders)
            VRChatHelper.OSC.SetCameraSlider(slider.Key, slider.Value);
    }

    /// <summary>
    /// Sends all mode, toggle, and slider values from a keyframe settings object.
    /// </summary>
    /// <param name="settings">Camera settings to send.</param>
    private static void ApplySettings(VRChatDollyCameraSettings settings)
    {
        if (settings.Mode.HasValue)
            VRChatHelper.OSC.SetCameraMode(settings.Mode.Value);

        foreach (var toggle in settings.Toggles)
            VRChatHelper.OSC.SetCameraToggle(toggle.Key, toggle.Value);

        foreach (var slider in settings.Sliders)
            VRChatHelper.OSC.SetCameraSlider(slider.Key, slider.Value);
    }

}
