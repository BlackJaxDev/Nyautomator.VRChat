using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Describes a request to play a stored VRChat dolly track.
/// </summary>
/// <param name="TrackId">Track identifier to play.</param>
/// <param name="RunMode">Loop behavior for playback.</param>
/// <param name="RepeatCount">Requested loop count when count-based playback is used.</param>
/// <param name="StartDelay">Optional delay before sending the first frame.</param>
/// <param name="FrameRate">Requested output frame rate, clamped by the playback service.</param>
/// <param name="StopGroup">Optional group name used to stop related playbacks together.</param>
/// <param name="SettingsApplyMode">Camera settings application behavior during playback.</param>
public sealed record VRChatDollyPlaybackRequest(
    string TrackId,
    VRChatDollyRunMode RunMode = VRChatDollyRunMode.Once,
    int RepeatCount = 1,
    TimeSpan? StartDelay = null,
    int FrameRate = 60,
    string? StopGroup = null,
    VRChatDollySettingsApplyMode SettingsApplyMode = VRChatDollySettingsApplyMode.AtKeyframes);
