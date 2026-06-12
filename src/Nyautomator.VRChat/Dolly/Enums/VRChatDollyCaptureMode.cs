using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Describes how a captured camera snapshot should be applied to a dolly track.
/// </summary>
public enum VRChatDollyCaptureMode
{
    /// <summary>
    /// Adds a new keyframe after the current last keyframe.
    /// </summary>
    Append,

    /// <summary>
    /// Adds a new keyframe at the requested track time.
    /// </summary>
    InsertAtTime,

    /// <summary>
    /// Uses the requested keyframe or creates one at the requested time, then applies the fields enabled by the capture request.
    /// </summary>
    ReplaceSelected,

    /// <summary>
    /// Uses the update path for a request that is expected to copy camera settings without replacing pose data.
    /// </summary>
    UpdateSettingsOnly,

    /// <summary>
    /// Uses the update path for a request that is expected to copy pose data without replacing camera settings.
    /// </summary>
    UpdatePoseOnly
}
