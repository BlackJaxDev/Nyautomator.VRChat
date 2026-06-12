using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Describes when captured camera settings should be sent while a dolly track plays.
/// </summary>
public enum VRChatDollySettingsApplyMode
{
    /// <summary>
    /// Applies settings when playback crosses stored keyframe times.
    /// </summary>
    AtKeyframes,

    /// <summary>
    /// Applies interpolated settings with every sent pose frame.
    /// </summary>
    EveryFrame,

    /// <summary>
    /// Applies the first keyframe's settings once at the start of each loop.
    /// </summary>
    TrackStartOnly,

    /// <summary>
    /// Sends camera pose only and leaves camera mode, toggles, and sliders untouched.
    /// </summary>
    PoseOnly
}
