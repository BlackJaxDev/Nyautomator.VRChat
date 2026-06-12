namespace Nyautomator;

/// <summary>
/// Payload raised when VRChat reports a user camera slider value over OSC.
/// </summary>
/// <param name="SliderName">Logical slider name resolved from the OSC address.</param>
/// <param name="Value">Slider value reported by VRChat.</param>
/// <param name="Address">Full OSC address that carried the slider value.</param>
public readonly record struct VRChatCameraSliderChangedEvent(
    string SliderName,
    float Value,
    string Address);
