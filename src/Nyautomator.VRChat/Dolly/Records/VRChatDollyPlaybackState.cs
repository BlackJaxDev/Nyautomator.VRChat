using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Snapshot of one active or recently stopped dolly playback session.
/// </summary>
/// <param name="PlaybackId">Unique playback session identifier.</param>
/// <param name="TrackId">Track being played.</param>
/// <param name="TrackName">Display name of the track being played.</param>
/// <param name="RunMode">Loop behavior selected for playback.</param>
/// <param name="RepeatCount">Normalized loop count for the session.</param>
/// <param name="CompletedLoops">Number of loops completed so far.</param>
/// <param name="FrameRate">Output frame rate used by playback.</param>
/// <param name="StopGroup">Optional stop group associated with the session.</param>
/// <param name="SettingsApplyMode">Settings application mode used by playback.</param>
/// <param name="StartedAtUtc">UTC time when the session started.</param>
/// <param name="StoppedAtUtc">UTC time when the session stopped, when known.</param>
/// <param name="TrackTimeSeconds">Latest track-relative time sent.</param>
/// <param name="IsRunning">Whether the session is still running.</param>
/// <param name="LastError">Last stop or failure reason associated with the session.</param>
public sealed record VRChatDollyPlaybackState(
    string PlaybackId,
    string TrackId,
    string TrackName,
    VRChatDollyRunMode RunMode,
    int RepeatCount,
    int CompletedLoops,
    int FrameRate,
    string? StopGroup,
    VRChatDollySettingsApplyMode SettingsApplyMode,
    DateTime StartedAtUtc,
    DateTime? StoppedAtUtc,
    double TrackTimeSeconds,
    bool IsRunning,
    string? LastError);
