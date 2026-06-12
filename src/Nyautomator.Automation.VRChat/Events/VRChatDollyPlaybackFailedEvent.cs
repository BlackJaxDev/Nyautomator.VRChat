namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Automation trigger fired when the VRChat dolly runtime reports a playback failure.
/// </summary>
public sealed class VRChatDollyPlaybackFailedEvent : VRChatDollyEventActionBase
{
    /// <summary>
    /// Stable automation type id used when dispatching playback-failed dolly events.
    /// </summary>
    public const string TypeId = "VRChat.Dolly.PlaybackFailed";

    /// <summary>
    /// Gets the dolly runtime event name accepted by this trigger.
    /// </summary>
    protected override string DollyEventType => "playbackFailed";

    /// <summary>
    /// Gets the automation registry id for this event node.
    /// </summary>
    public override string Id => TypeId;

    /// <summary>
    /// Gets the label shown for this event in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Playback Failed";

    /// <summary>
    /// Gets the user-facing explanation of when this event fires.
    /// </summary>
    public override string Description => "Triggers when VRChat dolly playback fails.";
}
