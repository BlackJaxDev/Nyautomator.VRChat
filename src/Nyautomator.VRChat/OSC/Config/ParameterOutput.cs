using Newtonsoft.Json;

namespace Nyautomator;

/// <summary>
/// OSC output binding metadata for an avatar parameter.
/// </summary>
/// <param name="Address">OSC address emitted by VRChat for this parameter.</param>
/// <param name="Type">VRChat parameter type, such as Int, Bool, or Float.</param>
public sealed record ParameterOutput(
    [property: JsonProperty("address")] string? Address,
    [property: JsonProperty("type")] string? Type);
