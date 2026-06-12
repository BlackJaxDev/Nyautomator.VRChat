using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// JSON-friendly representation of a quaternion used by stored dolly tracks.
/// </summary>
public sealed class VRChatDollyQuaternion
{
    /// <summary>
    /// Gets or sets the X component.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Gets or sets the Y component.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Gets or sets the Z component.
    /// </summary>
    public float Z { get; set; }

    /// <summary>
    /// Gets or sets the W component, defaulting to identity rotation.
    /// </summary>
    public float W { get; set; } = 1f;

    /// <summary>
    /// Creates a serializable dolly quaternion from a numerics quaternion.
    /// </summary>
    /// <param name="value">Quaternion value to copy.</param>
    /// <returns>A dolly quaternion containing the same components.</returns>
    public static VRChatDollyQuaternion From(Quaternion value)
        => new() { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

    /// <summary>
    /// Converts this serializable quaternion to a normalized numerics quaternion.
    /// </summary>
    /// <returns>A safe normalized <see cref="Quaternion"/>.</returns>
    public Quaternion ToQuaternion()
        => VRChatHelper.OSC.SafeNormalize(new Quaternion(X, Y, Z, W));
}
