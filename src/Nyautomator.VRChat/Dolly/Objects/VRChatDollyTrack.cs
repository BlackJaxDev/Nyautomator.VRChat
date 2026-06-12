using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// A persisted VRChat camera dolly track containing ordered keyframes and interpolation defaults.
/// </summary>
public sealed class VRChatDollyTrack
{
    /// <summary>
    /// Gets or sets the on-disk schema version for track normalization.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the stable track identifier used as the JSON file name.
    /// </summary>
    public string Id { get; set; } = VRChatDollyKeyframe.CreateId("track");

    /// <summary>
    /// Gets or sets the display name shown for the dolly track.
    /// </summary>
    public string Name { get; set; } = "New Dolly Track";

    /// <summary>
    /// Gets or sets the minimum playback duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; } = 5d;

    /// <summary>
    /// Gets or sets when the track was first created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets when the track was last saved.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the default position interpolation mode for segments without a keyframe override.
    /// </summary>
    public string DefaultPositionInterpolation { get; set; } = "catmullRom";

    /// <summary>
    /// Gets or sets the default rotation interpolation mode for segments without a keyframe override.
    /// </summary>
    public string DefaultRotationInterpolation { get; set; } = "slerp";

    /// <summary>
    /// Gets or sets the track keyframes, normalized to time order when stored.
    /// </summary>
    public List<VRChatDollyKeyframe> Keyframes { get; set; } = [];

    /// <summary>
    /// Creates a deep copy of this track and its keyframes.
    /// </summary>
    /// <returns>A cloned track instance.</returns>
    public VRChatDollyTrack Clone()
        => new()
        {
            SchemaVersion = SchemaVersion,
            Id = Id,
            Name = Name,
            DurationSeconds = DurationSeconds,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc,
            DefaultPositionInterpolation = DefaultPositionInterpolation,
            DefaultRotationInterpolation = DefaultRotationInterpolation,
            Keyframes = Keyframes.Select(k => k.Clone()).ToList()
        };
}
