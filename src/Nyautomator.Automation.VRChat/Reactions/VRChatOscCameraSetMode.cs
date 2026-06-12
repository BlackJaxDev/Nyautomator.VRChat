using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that switches the VRChat user camera mode over OSC.
/// </summary>
public class VRChatOscCameraSetMode : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the camera mode setter.
    /// </summary>
    public override string Id => "VRChat.OSC.Camera.SetMode";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Set Mode";

    /// <summary>
    /// Gets the user-facing explanation of what camera state this reaction changes.
    /// </summary>
    public override string Description => "Switches the VRChat user camera to the specified mode.";

    /// <summary>
    /// Gets or sets the camera mode name or numeric mode value to send.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraModeOptionsProvider))]
    public string Mode { get; set; } = "Photo";

    /// <summary>
    /// Resolves the configured camera mode by name first, then by numeric value.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Mode))
            return;

        if (VRChatHelper.OSC.TryGetCameraMode(Mode, out var definition))
        {
            VRChatHelper.OSC.SetCameraMode(definition.Mode);
            return;
        }

        if (int.TryParse(Mode, out var numericMode))
            VRChatHelper.OSC.SetCameraMode(numericMode);
    }
}
