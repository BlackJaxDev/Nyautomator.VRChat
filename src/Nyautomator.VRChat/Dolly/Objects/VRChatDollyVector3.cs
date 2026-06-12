using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// JSON-friendly representation of a 3D vector used by stored dolly tracks.
/// </summary>
public sealed class VRChatDollyVector3
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
    /// Creates a serializable dolly vector from a numerics vector.
    /// </summary>
    /// <param name="value">Vector value to copy.</param>
    /// <returns>A dolly vector containing the same components.</returns>
    public static VRChatDollyVector3 From(Vector3 value)
        => new() { X = value.X, Y = value.Y, Z = value.Z };

    /// <summary>
    /// Converts this serializable vector back to a numerics vector.
    /// </summary>
    /// <returns>A <see cref="Vector3"/> with the stored components.</returns>
    public Vector3 ToVector3() => new(X, Y, Z);
}
