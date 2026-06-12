namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired for each frame sent during a VRChat dolly playback.
/// </summary>
public sealed class VRChatDollyFrameSentEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching frame-sent dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.FrameSent";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "frameSent";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Frame Sent";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers periodically while Nyautomator is sending VRChat dolly frames.";
}
