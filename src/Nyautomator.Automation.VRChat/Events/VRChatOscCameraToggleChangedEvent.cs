using System;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when VRChat reports a camera toggle value over OSC.
/// </summary>
public sealed class VRChatOscCameraToggleChangedEvent : VRChatOscCameraEventActionBase<VRChatCameraToggleChangedEvent>
{
    /// <summary>
    /// Stable automation type id used when dispatching camera toggle events.
    /// </summary>
    public const string TypeId = "VRChat.OSC.Camera.ToggleChanged";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Toggle Changed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat reports a user camera toggle value.";

    /// <summary>
    /// Gets or sets the optional camera toggle name filter; blank accepts all toggles.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraToggleOptionsProvider))]
    public string Toggle { get; set; } = string.Empty;

    /// <summary>
    /// Filters incoming toggle payloads by toggle name when one is configured.
    /// </summary>
    /// <param name="payload">Camera toggle event payload raised by the OSC helper.</param>
    /// <returns><see langword="true"/> when the toggle filter is blank or matches the payload.</returns>
    protected override bool ShouldHandle(VRChatCameraToggleChangedEvent payload)
        => string.IsNullOrWhiteSpace(Toggle) || string.Equals(Toggle, payload.ToggleName, StringComparison.OrdinalIgnoreCase);
}
