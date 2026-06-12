using System.ComponentModel;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that captures the latest VRChat camera state into a Nyautomator dolly track.
/// </summary>
public sealed class VRChatDollyCaptureKeyframe : ReactionType, IAsyncReaction
{
    /// <summary>
    /// Gets the automation registry id for the dolly keyframe capture reaction.
    /// </summary>
    public override string Id => "VRChat.Dolly.CaptureKeyframe";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Capture Keyframe";

    /// <summary>
    /// Gets the user-facing explanation of what camera data this reaction captures.
    /// </summary>
    public override string Description => "Captures the latest VRChat camera pose and settings into a Nyautomator dolly track.";

    /// <summary>
    /// Gets or sets the Nyautomator dolly track that receives the captured keyframe.
    /// </summary>
    [Description("Nyautomator dolly track id.")]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the capture mode name parsed into <see cref="VRChatDollyCaptureMode"/>.
    /// </summary>
    [Description("Append, InsertAtTime, ReplaceSelected, UpdateSettingsOnly, or UpdatePoseOnly.")]
    public string CaptureMode { get; set; } = nameof(VRChatDollyCaptureMode.Append);

    /// <summary>
    /// Gets or sets the optional timeline position used by capture modes that target a time.
    /// </summary>
    public double? TimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the optional keyframe id used by capture modes that update or replace a keyframe.
    /// </summary>
    public string? KeyframeId { get; set; }

    /// <summary>
    /// Gets or sets whether the current camera pose is included in the capture request.
    /// </summary>
    public bool IncludePose { get; set; } = true;

    /// <summary>
    /// Gets or sets whether current camera settings are included in the capture request.
    /// </summary>
    public bool IncludeSettings { get; set; } = true;

    /// <summary>
    /// Executes the asynchronous capture synchronously for the automation engine.
    /// </summary>
    public override void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Parses capture settings and asks the dolly runtime to capture a keyframe for the configured track.
    /// </summary>
    /// <returns>A task that completes after the capture request has been sent.</returns>
    public async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(TrackId))
            return;

        var mode = !string.IsNullOrWhiteSpace(CaptureMode)
            && Enum.TryParse<VRChatDollyCaptureMode>(CaptureMode, true, out var parsed)
                ? parsed
                : VRChatDollyCaptureMode.Append;

        await VRChatDollyRuntime.CaptureKeyframeAsync(new VRChatDollyCaptureRequest(
            TrackId.Trim(),
            mode,
            TimeSeconds,
            KeyframeId,
            IncludePose,
            IncludeSettings)).ConfigureAwait(false);
    }
}
