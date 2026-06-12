using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when VRChat reports user camera position and rotation over OSC.
/// </summary>
public sealed class VRChatOscCameraPoseChangedEvent : VRChatOscCameraEventActionBase<VRChatCameraPoseChangedEvent>
{
    /// <summary>
    /// Stable automation type id used when dispatching camera pose events.
    /// </summary>
    public const string TypeId = "VRChat.OSC.Camera.PoseChanged";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Pose Changed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat reports the user camera position and rotation.";
}
