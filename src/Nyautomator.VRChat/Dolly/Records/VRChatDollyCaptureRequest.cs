using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Describes how the current VRChat camera snapshot should be captured into a track.
/// </summary>
/// <param name="TrackId">Target track identifier.</param>
/// <param name="Mode">Capture behavior to use for the keyframe.</param>
/// <param name="TimeSeconds">Optional target time for insert or update operations.</param>
/// <param name="KeyframeId">Optional keyframe identifier to replace or update.</param>
/// <param name="IncludePose">Whether the current camera pose should be captured.</param>
/// <param name="IncludeSettings">Whether the current camera settings should be captured.</param>
public sealed record VRChatDollyCaptureRequest(
    string TrackId,
    VRChatDollyCaptureMode Mode = VRChatDollyCaptureMode.Append,
    double? TimeSeconds = null,
    string? KeyframeId = null,
    bool IncludePose = true,
    bool IncludeSettings = true);
