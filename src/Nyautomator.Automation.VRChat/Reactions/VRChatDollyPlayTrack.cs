using System.ComponentModel;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that plays a Nyautomator VRChat dolly track through the camera OSC runtime.
/// </summary>
public sealed class VRChatDollyPlayTrack : ReactionType, IAsyncReaction
{
    /// <summary>
    /// Gets the automation registry id for the dolly playback reaction.
    /// </summary>
    public override string Id => "VRChat.Dolly.PlayTrack";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Dolly: Play Track";

    /// <summary>
    /// Gets the user-facing explanation of what the dolly playback runtime does.
    /// </summary>
    public override string Description => "Plays a Nyautomator-owned VRChat camera dolly track by driving the user camera over OSC.";

    /// <summary>
    /// Gets or sets the Nyautomator dolly track id to play.
    /// </summary>
    [Description("Nyautomator dolly track id.")]
    public string TrackId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the playback mode name parsed into <see cref="VRChatDollyRunMode"/>.
    /// </summary>
    [Description("Once, Count, or UntilStopped.")]
    public string RunMode { get; set; } = nameof(VRChatDollyRunMode.Once);

    /// <summary>
    /// Gets or sets the requested repeat count used when the run mode supports counted playback.
    /// </summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the non-negative delay applied before playback begins.
    /// </summary>
    public double StartDelaySeconds { get; set; }

    /// <summary>
    /// Gets or sets the frame rate requested for interpolated camera frames.
    /// </summary>
    public int FrameRate { get; set; } = 60;

    /// <summary>
    /// Gets or sets an optional group token that can be used by stop requests.
    /// </summary>
    public string? StopGroup { get; set; }

    /// <summary>
    /// Gets or sets the settings application mode parsed into <see cref="VRChatDollySettingsApplyMode"/>.
    /// </summary>
    [Description("AtKeyframes, EveryFrame, TrackStartOnly, or PoseOnly.")]
    public string SettingsApplyMode { get; set; } = nameof(VRChatDollySettingsApplyMode.AtKeyframes);

    /// <summary>
    /// Executes asynchronous dolly playback synchronously for the automation engine.
    /// </summary>
    public override void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Builds a playback request from the configured properties and sends it to the dolly runtime.
    /// </summary>
    /// <returns>A task that completes after playback has been requested.</returns>
    public async Task ExecuteAsync()
    {
        if (string.IsNullOrWhiteSpace(TrackId))
            return;

        var runMode = ParseEnum(RunMode, VRChatDollyRunMode.Once);
        var settingsMode = ParseEnum(SettingsApplyMode, VRChatDollySettingsApplyMode.AtKeyframes);

        await VRChatDollyRuntime.PlayTrackAsync(new VRChatDollyPlaybackRequest(
            TrackId.Trim(),
            runMode,
            RepeatCount,
            TimeSpan.FromSeconds(Math.Max(0d, StartDelaySeconds)),
            FrameRate,
            StopGroup,
            settingsMode)).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses an enum setting by name and returns the supplied fallback when the value is blank or invalid.
    /// </summary>
    /// <typeparam name="TEnum">Enum type to parse.</typeparam>
    /// <param name="value">Raw enum name from a reaction property.</param>
    /// <param name="fallback">Value to use when parsing fails.</param>
    /// <returns>The parsed enum value or the fallback.</returns>
    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value, true, out var parsed)
            ? parsed
            : fallback;
}
