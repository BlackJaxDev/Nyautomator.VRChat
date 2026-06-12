using Newtonsoft.Json;

namespace Nyautomator;

/// <summary>
/// OSC input binding metadata for an avatar parameter.
/// </summary>
/// <param name="Address">OSC address accepted by VRChat for this parameter.</param>
/// <param name="Type">VRChat parameter type, such as Int, Bool, or Float.</param>
public sealed record ParameterInput(
    [property: JsonProperty("address")] string? Address,
    [property: JsonProperty("type")] string? Type);
