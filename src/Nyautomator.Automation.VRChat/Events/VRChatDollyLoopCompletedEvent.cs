namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when a loop iteration of VRChat dolly playback finishes.
/// </summary>
public sealed class VRChatDollyLoopCompletedEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching loop-completed dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.LoopCompleted";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "loopCompleted";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Loop Completed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when a VRChat dolly playback loop completes.";
}
