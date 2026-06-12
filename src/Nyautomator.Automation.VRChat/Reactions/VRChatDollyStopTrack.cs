using System.ComponentModel;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that stops one or more active VRChat dolly playback sessions.
/// </summary>
public sealed class VRChatDollyStopTrack : ReactionType, IAsyncReaction
{
    /// <summary>
    /// Gets the automation registry id for the dolly stop reaction.
    /// </summary>
    public override string Id => "VRChat.Dolly.StopTrack";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Stop Track";

    /// <summary>
    /// Gets the user-facing explanation of what playback sessions this reaction can stop.
    /// </summary>
    public override string Description => "Stops one or more Nyautomator VRChat dolly playback sessions.";

    /// <summary>
    /// Gets or sets an optional track id filter for the stop request.
    /// </summary>
    [Description("Optional track id. Leave blank to stop all matching tracks.")]
    public string? TrackId { get; set; }

    /// <summary>
    /// Gets or sets an optional stop group filter for the stop request.
    /// </summary>
    [Description("Optional stop group. Leave blank to ignore groups.")]
    public string? StopGroup { get; set; }

    /// <summary>
    /// Executes the asynchronous stop request synchronously for the automation engine.
    /// </summary>
    public override void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Sends the configured track and group filters to the dolly runtime stop operation.
    /// </summary>
    /// <returns>A task that completes after the stop request has been sent.</returns>
    public async Task ExecuteAsync()
    {
        await VRChatDollyRuntime.StopTrackAsync(TrackId, StopGroup).ConfigureAwait(false);
    }
}
