using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// A single pose and camera settings sample within a VRChat dolly track.
/// </summary>
public sealed class VRChatDollyKeyframe
{
    /// <summary>
    /// Gets or sets the stable keyframe identifier.
    /// </summary>
    public string Id { get; set; } = CreateId("kf");

    /// <summary>
    /// Gets or sets the keyframe time within the track, in seconds.
    /// </summary>
    public double TimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the camera position captured for this keyframe.
    /// </summary>
    public VRChatDollyVector3 Position { get; set; } = new();

    /// <summary>
    /// Gets or sets the camera rotation expressed as Euler degrees.
    /// </summary>
    public VRChatDollyVector3 EulerDegrees { get; set; } = new();

    /// <summary>
    /// Gets or sets the camera rotation expressed as a quaternion.
    /// </summary>
    public VRChatDollyQuaternion Rotation { get; set; } = new();

    /// <summary>
    /// Gets or sets the camera settings captured for this keyframe.
    /// </summary>
    public VRChatDollyCameraSettings Camera { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional position interpolation override starting at this keyframe.
    /// </summary>
    public string? PositionInterpolation { get; set; }

    /// <summary>
    /// Gets or sets the optional rotation interpolation override starting at this keyframe.
    /// </summary>
    public string? RotationInterpolation { get; set; }

    /// <summary>
    /// Creates a deep copy of this keyframe while preserving its identifier.
    /// </summary>
    /// <returns>A cloned keyframe instance.</returns>
    public VRChatDollyKeyframe Clone()
        => new()
        {
            Id = Id,
            TimeSeconds = TimeSeconds,
            Position = VRChatDollyVector3.From(Position.ToVector3()),
            EulerDegrees = VRChatDollyVector3.From(EulerDegrees.ToVector3()),
            Rotation = VRChatDollyQuaternion.From(Rotation.ToQuaternion()),
            Camera = Camera.Clone(),
            PositionInterpolation = PositionInterpolation,
            RotationInterpolation = RotationInterpolation
        };

    /// <summary>
    /// Creates a unique identifier using the supplied prefix.
    /// </summary>
    /// <param name="prefix">Identifier prefix such as kf, track, or playback.</param>
    /// <returns>A prefix plus a compact GUID.</returns>
    internal static string CreateId(string prefix)
        => $"{prefix}_{Guid.NewGuid():N}";
}
