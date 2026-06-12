using System.Numerics;

namespace Nyautomator;

/// <summary>
/// Payload raised when VRChat reports the user camera pose over OSC.
/// </summary>
/// <param name="Position">Camera position vector reported by VRChat.</param>
/// <param name="Rotation">Camera rotation quaternion, normalized before dispatch.</param>
/// <param name="Address">Full OSC address that carried the pose.</param>
/// <param name="EulerDegrees">Camera rotation expressed as Euler degrees.</param>
/// <param name="RawValues">Raw float arguments read from the OSC message.</param>
public readonly record struct VRChatCameraPoseChangedEvent(
    Vector3 Position,
    Quaternion Rotation,
    string Address,
    Vector3 EulerDegrees,
    IReadOnlyList<float> RawValues);
