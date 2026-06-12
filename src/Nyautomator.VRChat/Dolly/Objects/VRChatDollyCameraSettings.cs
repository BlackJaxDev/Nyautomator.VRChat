using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Camera mode, toggle, and slider values captured on a dolly keyframe.
/// </summary>
public sealed class VRChatDollyCameraSettings
{
    /// <summary>
    /// Gets or sets the numeric VRChat user camera mode.
    /// </summary>
    public int? Mode { get; set; }

    /// <summary>
    /// Gets or sets the resolved VRChat user camera mode name.
    /// </summary>
    public string? ModeName { get; set; }

    /// <summary>
    /// Gets or sets camera slider values keyed by logical slider name.
    /// </summary>
    public Dictionary<string, float> Sliders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets camera toggle values keyed by logical toggle name.
    /// </summary>
    public Dictionary<string, bool> Toggles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a deep copy of the camera settings dictionaries.
    /// </summary>
    /// <returns>A cloned camera settings instance.</returns>
    public VRChatDollyCameraSettings Clone()
        => new()
        {
            Mode = Mode,
            ModeName = ModeName,
            Sliders = new Dictionary<string, float>(Sliders, StringComparer.OrdinalIgnoreCase),
            Toggles = new Dictionary<string, bool>(Toggles, StringComparer.OrdinalIgnoreCase)
        };
}
