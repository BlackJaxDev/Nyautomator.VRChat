using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// A sampled frame ready to be sent to the VRChat user camera over OSC.
/// </summary>
/// <param name="TrackTime">Track-relative time represented by the frame.</param>
/// <param name="Position">Camera position to send.</param>
/// <param name="EulerDegrees">Camera rotation in Euler degrees to send.</param>
/// <param name="Rotation">Camera rotation as a quaternion.</param>
/// <param name="Mode">Optional user camera mode to send.</param>
/// <param name="Toggles">Camera toggles to send when settings are applied.</param>
/// <param name="Sliders">Camera sliders to send when settings are applied.</param>
public sealed record VRChatDollyFrame(
    TimeSpan TrackTime,
    Vector3 Position,
    Vector3 EulerDegrees,
    Quaternion Rotation,
    int? Mode,
    IReadOnlyDictionary<string, bool> Toggles,
    IReadOnlyDictionary<string, float> Sliders);
