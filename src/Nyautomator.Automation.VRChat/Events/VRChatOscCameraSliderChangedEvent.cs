using System;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when VRChat reports a camera slider value over OSC.
/// </summary>
public sealed class VRChatOscCameraSliderChangedEvent : VRChatOscCameraEventActionBase<VRChatCameraSliderChangedEvent>
{
    /// <summary>
    /// Stable automation type id used when dispatching camera slider events.
    /// </summary>
    public const string TypeId = "VRChat.OSC.Camera.SliderChanged";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Slider Changed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat reports a user camera slider value.";

    /// <summary>
    /// Gets or sets the optional camera slider name filter; blank accepts all sliders.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraSliderOptionsProvider))]
    public string Slider { get; set; } = string.Empty;

    /// <summary>
    /// Filters incoming slider payloads by slider name when one is configured.
    /// </summary>
    /// <param name="payload">Camera slider event payload raised by the OSC helper.</param>
    /// <returns><see langword="true"/> when the slider filter is blank or matches the payload.</returns>
    protected override bool ShouldHandle(VRChatCameraSliderChangedEvent payload)
        => string.IsNullOrWhiteSpace(Slider) || string.Equals(Slider, payload.SliderName, StringComparison.OrdinalIgnoreCase);
}
