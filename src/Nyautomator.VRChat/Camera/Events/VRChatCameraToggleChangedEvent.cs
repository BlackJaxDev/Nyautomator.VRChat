namespace Nyautomator;

/// <summary>
/// Payload raised when VRChat reports a user camera toggle value over OSC.
/// </summary>
/// <param name="ToggleName">Logical toggle name resolved from the OSC address.</param>
/// <param name="Enabled">Toggle state converted using OSC boolean semantics.</param>
/// <param name="Address">Full OSC address that carried the toggle value.</param>
/// <param name="RawValue">Raw numeric OSC value used to derive <paramref name="Enabled"/>.</param>
public readonly record struct VRChatCameraToggleChangedEvent(
    string ToggleName,
    bool Enabled,
    string Address,
    float RawValue);
