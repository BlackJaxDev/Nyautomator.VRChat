namespace Nyautomator;

/// <summary>
/// Payload raised when an incoming OSC avatar parameter message updates the local parameter cache.
/// </summary>
/// <param name="ParameterName">Avatar parameter name without the OSC address prefix.</param>
/// <param name="Address">Full OSC address that carried the update.</param>
/// <param name="ParameterType">VRChat parameter type reported by avatar metadata, or Unknown when not matched.</param>
/// <param name="Value">Raw OSC argument value from the message.</param>
/// <param name="BoolValue">Raw value converted using OSC boolean semantics.</param>
/// <param name="IntValue">Raw numeric value rounded to an integer.</param>
/// <param name="FloatValue">Raw numeric value converted to a float.</param>
public readonly record struct VRChatAvatarParameterChangedEvent(
    string ParameterName,
    string Address,
    string ParameterType,
    object? Value,
    bool BoolValue,
    int IntValue,
    float FloatValue);
