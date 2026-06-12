namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired after Nyautomator captures a camera state into a dolly track.
/// </summary>
public sealed class VRChatDollyKeyframeCapturedEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching keyframe-captured dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.KeyframeCaptured";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "keyframeCaptured";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Keyframe Captured";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when Nyautomator captures a VRChat dolly keyframe.";
}
