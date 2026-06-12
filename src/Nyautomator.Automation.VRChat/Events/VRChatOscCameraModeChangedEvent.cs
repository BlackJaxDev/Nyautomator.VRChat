using System;
using System.Globalization;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when VRChat reports a user camera mode change over OSC.
/// </summary>
public sealed class VRChatOscCameraModeChangedEvent : VRChatOscCameraEventActionBase<VRChatCameraModeChangedEvent>
{
    /// <summary>
    /// Stable automation type id used when dispatching camera mode events.
    /// </summary>
    public const string TypeId = "VRChat.OSC.Camera.ModeChanged";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Mode Changed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat reports a user camera mode change.";

    /// <summary>
    /// Gets or sets the optional camera mode name or numeric mode filter; blank accepts all modes.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraModeOptionsProvider))]
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Filters incoming mode payloads by mode name or numeric mode value.
    /// </summary>
    /// <param name="payload">Camera mode event payload raised by the OSC helper.</param>
    /// <returns><see langword="true"/> when the configured filter is blank or matches the payload.</returns>
    protected override bool ShouldHandle(VRChatCameraModeChangedEvent payload)
    {
        if (string.IsNullOrWhiteSpace(Mode))
            return true;

        if (string.Equals(Mode, payload.ModeName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (int.TryParse(Mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            return numeric == payload.Mode;

        return false;
    }
}
