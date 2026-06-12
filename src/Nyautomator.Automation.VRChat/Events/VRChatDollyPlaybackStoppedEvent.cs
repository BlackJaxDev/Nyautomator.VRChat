namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when Nyautomator stops a VRChat dolly playback session.
/// </summary>
public sealed class VRChatDollyPlaybackStoppedEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching playback-stopped dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.PlaybackStopped";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "playbackStopped";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Playback Stopped";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when Nyautomator stops VRChat dolly playback.";
}
