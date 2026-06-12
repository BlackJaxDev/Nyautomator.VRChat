using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that sets a VRChat user camera toggle over OSC.
/// </summary>
public class VRChatOscCameraSetToggle : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the camera toggle setter.
    /// </summary>
    public override string Id => "VRChat.OSC.Camera.SetToggle";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Set Toggle";

    /// <summary>
    /// Gets the user-facing explanation of what camera state this reaction changes.
    /// </summary>
    public override string Description => "Sets a boolean toggle on the VRChat user camera.";

    /// <summary>
    /// Gets or sets the named camera toggle to update.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraToggleOptionsProvider))]
    public string Toggle { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the boolean value sent for the selected toggle.
    /// </summary>
    public bool Value { get; set; } = true;

    /// <summary>
    /// Sends the configured toggle state when a toggle name is present.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Toggle))
            return;

        VRChatHelper.OSC.SetCameraToggle(Toggle, Value);
    }
}
