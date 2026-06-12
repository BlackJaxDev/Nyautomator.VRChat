using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Persists VRChat dolly tracks as JSON files in a configured directory.
/// </summary>
public sealed class VRChatDollyTrackStore
{
    /// <summary>
    /// JSON serializer settings used for readable track files and null omission.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Synchronizes file writes and deletes within this store instance.
    /// </summary>
    private readonly object _sync = new();

    /// <summary>
    /// Creates a track store and ensures the storage directory exists.
    /// </summary>
    /// <param name="trackDirectory">Directory where track JSON files should be stored, or blank for the default settings path.</param>
    public VRChatDollyTrackStore(string trackDirectory)
    {
        TrackDirectory = string.IsNullOrWhiteSpace(trackDirectory)
            ? VRChatDollyOptions.GetStandaloneDefaultTrackDirectory()
            : Path.GetFullPath(trackDirectory);

        Directory.CreateDirectory(TrackDirectory);
    }

    /// <summary>
    /// Gets the absolute directory where dolly track JSON files are stored.
    /// </summary>
    public string TrackDirectory { get; }

    /// <summary>
    /// Loads all readable track JSON files, normalizes them, and orders them by recent update.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for file enumeration and reads.</param>
    /// <returns>Normalized tracks from the store directory.</returns>
    public async Task<IReadOnlyList<VRChatDollyTrack>> ListTracksAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(TrackDirectory);
        var tracks = new List<VRChatDollyTrack>();

        foreach (var file in Directory.EnumerateFiles(TrackDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var track = JsonSerializer.Deserialize<VRChatDollyTrack>(text, JsonOptions);
                if (track is not null)
                    tracks.Add(NormalizeTrack(track));
            }
            catch (Exception ex)
            {
                VRChatDollyRuntime.Emit("trackLoadFailed", null, $"Failed to load VRChat dolly track '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        return tracks
            .OrderByDescending(t => t.UpdatedAtUtc)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Loads and normalizes a single track by identifier.
    /// </summary>
    /// <param name="trackId">Track identifier to load.</param>
    /// <param name="cancellationToken">Cancellation token for the file read.</param>
    /// <returns>The normalized track when its JSON file exists; otherwise <see langword="null"/>.</returns>
    public async Task<VRChatDollyTrack?> GetTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var normalizedId = NormalizeId(trackId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return null;

        var path = GetTrackPath(normalizedId);
        if (!File.Exists(path))
            return null;

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var track = JsonSerializer.Deserialize<VRChatDollyTrack>(text, JsonOptions);
        return track is null ? null : NormalizeTrack(track);
    }

    /// <summary>
    /// Normalizes and writes a track JSON file, then returns a clone of the saved track.
    /// </summary>
    /// <param name="track">Track to persist.</param>
    /// <param name="cancellationToken">Cancellation token checked after the synchronous file write.</param>
    /// <returns>A clone of the normalized track.</returns>
    public async Task<VRChatDollyTrack> SaveTrackAsync(VRChatDollyTrack track, CancellationToken cancellationToken = default)
    {
        if (track is null)
            throw new ArgumentNullException(nameof(track));

        track = NormalizeTrack(track);
        track.UpdatedAtUtc = DateTime.UtcNow;

        Directory.CreateDirectory(TrackDirectory);
        var path = GetTrackPath(track.Id);
        var json = JsonSerializer.Serialize(track, JsonOptions);

        lock (_sync)
        {
            File.WriteAllText(path, json);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return track.Clone();
    }

    /// <summary>
    /// Deletes a track JSON file by identifier.
    /// </summary>
    /// <param name="trackId">Track identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token checked before deletion.</param>
    /// <returns><see langword="true"/> when an existing file was deleted.</returns>
    public Task<bool> DeleteTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedId = NormalizeId(trackId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            return Task.FromResult(false);

        var path = GetTrackPath(normalizedId);
        if (!File.Exists(path))
            return Task.FromResult(false);

        lock (_sync)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Clones an existing track, assigns fresh identifiers, and saves the copy.
    /// </summary>
    /// <param name="trackId">Source track identifier.</param>
    /// <param name="cancellationToken">Cancellation token for load and save operations.</param>
    /// <returns>The saved copy, or <see langword="null"/> when the source is missing.</returns>
    public async Task<VRChatDollyTrack?> DuplicateTrackAsync(string trackId, CancellationToken cancellationToken = default)
    {
        var source = await GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
        if (source is null)
            return null;

        var now = DateTime.UtcNow;
        var copy = source.Clone();
        copy.Id = VRChatDollyKeyframe.CreateId("track");
        copy.Name = $"{source.Name} Copy";
        copy.CreatedAtUtc = now;
        copy.UpdatedAtUtc = now;
        foreach (var keyframe in copy.Keyframes)
            keyframe.Id = VRChatDollyKeyframe.CreateId("kf");

        return await SaveTrackAsync(copy, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the JSON file path for a normalized track identifier.
    /// </summary>
    /// <param name="trackId">Track identifier to normalize and use as a file name.</param>
    /// <returns>Absolute path for the track JSON file.</returns>
    /// <exception cref="ArgumentException">Thrown when the identifier is blank after normalization.</exception>
    public string GetTrackPath(string trackId)
    {
        var normalizedId = NormalizeId(trackId);
        if (string.IsNullOrWhiteSpace(normalizedId))
            throw new ArgumentException("Invalid track id.", nameof(trackId));

        return Path.Combine(TrackDirectory, $"{normalizedId}.json");
    }

    /// <summary>
    /// Repairs defaults, identifiers, ordering, rotations, and dictionaries on a loaded or incoming track.
    /// </summary>
    /// <param name="track">Track to normalize in place.</param>
    /// <returns>The normalized track.</returns>
    private static VRChatDollyTrack NormalizeTrack(VRChatDollyTrack track)
    {
        var now = DateTime.UtcNow;
        track.SchemaVersion = track.SchemaVersion <= 0 ? 1 : track.SchemaVersion;
        track.Id = NormalizeId(track.Id);
        if (string.IsNullOrWhiteSpace(track.Id))
            track.Id = VRChatDollyKeyframe.CreateId("track");
        if (string.IsNullOrWhiteSpace(track.Name))
            track.Name = "Untitled Dolly Track";
        if (track.CreatedAtUtc == default)
            track.CreatedAtUtc = now;
        if (track.UpdatedAtUtc == default)
            track.UpdatedAtUtc = now;
        track.DurationSeconds = Math.Max(0d, track.DurationSeconds);
        track.DefaultPositionInterpolation = string.IsNullOrWhiteSpace(track.DefaultPositionInterpolation)
            ? "catmullRom"
            : track.DefaultPositionInterpolation;
        track.DefaultRotationInterpolation = string.IsNullOrWhiteSpace(track.DefaultRotationInterpolation)
            ? "slerp"
            : track.DefaultRotationInterpolation;

        track.Keyframes ??= [];
        foreach (var keyframe in track.Keyframes)
        {
            if (string.IsNullOrWhiteSpace(keyframe.Id))
                keyframe.Id = VRChatDollyKeyframe.CreateId("kf");
            keyframe.TimeSeconds = Math.Max(0d, keyframe.TimeSeconds);
            var hadEuler = keyframe.EulerDegrees is not null;
            var hadRotation = keyframe.Rotation is not null;
            keyframe.Position ??= new VRChatDollyVector3();
            keyframe.EulerDegrees ??= new VRChatDollyVector3();
            keyframe.Rotation ??= VRChatDollyQuaternion.From(VRChatHelper.OSC.EulerDegreesToQuaternion(keyframe.EulerDegrees.ToVector3()));
            if (!hadEuler && hadRotation)
                keyframe.EulerDegrees = VRChatDollyVector3.From(VRChatHelper.OSC.QuaternionToEulerDegrees(keyframe.Rotation.ToQuaternion()));
            else
                keyframe.Rotation = VRChatDollyQuaternion.From(VRChatHelper.OSC.EulerDegreesToQuaternion(keyframe.EulerDegrees.ToVector3()));
            keyframe.Camera ??= new VRChatDollyCameraSettings();
            keyframe.Camera.Sliders = NormalizeDictionary(keyframe.Camera.Sliders);
            keyframe.Camera.Toggles = NormalizeDictionary(keyframe.Camera.Toggles);
        }

        track.Keyframes = track.Keyframes.OrderBy(k => k.TimeSeconds).ToList();
        if (track.Keyframes.Count > 0)
            track.DurationSeconds = Math.Max(track.DurationSeconds, track.Keyframes.Max(k => k.TimeSeconds));

        return track;
    }

    /// <summary>
    /// Copies a dictionary into a case-insensitive dictionary while trimming nonblank keys.
    /// </summary>
    /// <typeparam name="TValue">Dictionary value type.</typeparam>
    /// <param name="values">Source dictionary, or null.</param>
    /// <returns>A normalized dictionary.</returns>
    private static Dictionary<string, TValue> NormalizeDictionary<TValue>(IDictionary<string, TValue>? values)
    {
        var normalized = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);
        if (values is null)
            return normalized;

        foreach (var pair in values)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
                normalized[pair.Key.Trim()] = pair.Value;
        }

        return normalized;
    }

    /// <summary>
    /// Strips a track identifier down to characters that are safe to use in a file name.
    /// </summary>
    /// <param name="id">Raw track identifier.</param>
    /// <returns>Trimmed identifier containing only letters, digits, underscores, and hyphens.</returns>
    private static string NormalizeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return string.Empty;

        var chars = id.Trim()
            .Where(c => char.IsLetterOrDigit(c) || c is '_' or '-')
            .ToArray();

        return new string(chars);
    }
}
