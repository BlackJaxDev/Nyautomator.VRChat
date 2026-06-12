using System.Numerics;

namespace Nyautomator;

/// <summary>
/// Immutable snapshot of the most recent user camera pose observed from VRChat OSC.
/// </summary>
/// <param name="Position">Camera position vector.</param>
/// <param name="EulerDegrees">Camera rotation expressed as Euler degrees.</param>
/// <param name="Rotation">Camera rotation quaternion.</param>
/// <param name="RawValues">Raw OSC float arguments used to build the snapshot.</param>
/// <param name="UpdatedAtUtc">UTC time when the pose was observed.</param>
public sealed record VRChatCameraPoseSnapshot(
    Vector3 Position,
    Vector3 EulerDegrees,
    Quaternion Rotation,
    IReadOnlyList<float> RawValues,
    DateTime UpdatedAtUtc);
