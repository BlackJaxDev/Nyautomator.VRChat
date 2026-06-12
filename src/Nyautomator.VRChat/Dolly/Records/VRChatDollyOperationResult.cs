using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Result returned by dolly operations that can fail without throwing.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Message">Human-readable operation result.</param>
/// <param name="Error">Error text when the operation failed.</param>
/// <param name="Track">Related track when available.</param>
/// <param name="Playback">Related playback state when available.</param>
/// <param name="Keyframe">Related keyframe when available.</param>
public sealed record VRChatDollyOperationResult(
    bool Success,
    string Message,
    string? Error = null,
    VRChatDollyTrack? Track = null,
    VRChatDollyPlaybackState? Playback = null,
    VRChatDollyKeyframe? Keyframe = null);
