using Newtonsoft.Json;

namespace Nyautomator;

/// <summary>
/// Avatar parameter entry from a VRChat OSC avatar configuration file.
/// </summary>
/// <param name="Name">Parameter name used by the avatar.</param>
/// <param name="Input">OSC input binding accepted by VRChat for this parameter.</param>
/// <param name="Output">OSC output binding emitted by VRChat for this parameter.</param>
public sealed record AvatarParameter(
    [property: JsonProperty("name")] string? Name,
    [property: JsonProperty("input")] ParameterInput? Input,
    [property: JsonProperty("output")] ParameterOutput? Output);
