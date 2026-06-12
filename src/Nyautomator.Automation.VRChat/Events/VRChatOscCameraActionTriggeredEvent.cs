using System;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when VRChat reports a user camera action over OSC.
/// </summary>
public sealed class VRChatOscCameraActionTriggeredEvent : VRChatOscCameraEventActionBase<VRChatCameraActionTriggeredEvent>
{
    /// <summary>
    /// Stable automation type id used when dispatching camera action events.
    /// </summary>
    public const string TypeId = "VRChat.OSC.Camera.ActionTriggered";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Action Triggered";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat reports a user camera action.";

    /// <summary>
    /// Gets or sets the optional camera action name filter; blank accepts all actions.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraActionOptionsProvider))]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Filters incoming action payloads by camera action name when one is configured.
    /// </summary>
    /// <param name="payload">Camera action event payload raised by the OSC helper.</param>
    /// <returns><see langword="true"/> when the action name is blank or matches the payload.</returns>
    protected override bool ShouldHandle(VRChatCameraActionTriggeredEvent payload)
        => string.IsNullOrWhiteSpace(Action) || string.Equals(Action, payload.ActionName, StringComparison.OrdinalIgnoreCase);
}
