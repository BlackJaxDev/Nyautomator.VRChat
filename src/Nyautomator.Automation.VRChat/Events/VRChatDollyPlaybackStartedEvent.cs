namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when Nyautomator starts a VRChat dolly playback session.
/// </summary>
public sealed class VRChatDollyPlaybackStartedEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching playback-started dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.PlaybackStarted";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "playbackStarted";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Playback Started";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when Nyautomator starts VRChat dolly playback.";
}
