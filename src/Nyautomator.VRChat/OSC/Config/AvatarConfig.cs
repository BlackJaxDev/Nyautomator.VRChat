using Newtonsoft.Json;

namespace Nyautomator;

/// <summary>
/// VRChat OSC avatar configuration loaded from the avatar config JSON file.
/// </summary>
/// <param name="Id">VRChat avatar id associated with the configuration.</param>
/// <param name="Name">Display name of the avatar.</param>
/// <param name="Parameters">Avatar parameters exposed for OSC input or output.</param>
public sealed record AvatarConfig(
    [property: JsonProperty("id")] string? Id,
    [property: JsonProperty("name")] string? Name,
    [property: JsonProperty("parameters")] AvatarParameter[]? Parameters);
