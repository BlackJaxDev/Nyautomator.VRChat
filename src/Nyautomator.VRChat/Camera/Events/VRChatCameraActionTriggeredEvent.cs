namespace Nyautomator;

/// <summary>
/// Payload raised when VRChat reports a user camera action OSC message.
/// </summary>
/// <param name="ActionName">Logical camera action name resolved from the OSC address.</param>
/// <param name="Value">Numeric action payload from the OSC message.</param>
/// <param name="Address">Full OSC address that carried the action.</param>
public readonly record struct VRChatCameraActionTriggeredEvent(
    string ActionName,
    float Value,
    string Address);
