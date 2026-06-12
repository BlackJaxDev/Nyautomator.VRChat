namespace Nyautomator;

/// <summary>
/// Payload raised when VRChat reports a user camera mode OSC message.
/// </summary>
/// <param name="Mode">Numeric camera mode value reported by VRChat.</param>
/// <param name="ModeName">Known camera mode name resolved from the numeric mode.</param>
/// <param name="Address">Full OSC address that carried the mode value.</param>
/// <param name="RawValue">Raw numeric OSC value before rounding to a mode integer.</param>
public readonly record struct VRChatCameraModeChangedEvent(
    int Mode,
    string ModeName,
    string Address,
    float RawValue);
