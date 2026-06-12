using System.Globalization;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that triggers a VRChat user camera action over OSC.
/// </summary>
public class VRChatOscCameraAction : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the camera action trigger.
    /// </summary>
    public override string Id => "VRChat.OSC.Camera.Action";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Action";

    /// <summary>
    /// Gets the user-facing explanation of which VRChat camera actions this reaction can trigger.
    /// </summary>
    public override string Description => "Triggers one of the VRChat user camera actions (close, capture, capture delayed).";

    /// <summary>
    /// Gets or sets the named camera action to trigger.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraActionOptionsProvider))]
    public string Action { get; set; } = "Capture";

    /// <summary>
    /// Gets or sets the numeric OSC payload sent with the action; invalid values fall back to 1.
    /// </summary>
    public string Payload { get; set; } = "1";

    /// <summary>
    /// Parses the optional payload and sends the configured camera action to VRChat.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Action))
            return;

        var payload = 1f;
        if (!string.IsNullOrWhiteSpace(Payload) && TryParse(Payload, out var parsed))
            payload = parsed;

        VRChatHelper.OSC.TriggerCameraAction(Action, payload);
    }

    /// <summary>
    /// Parses payload text as a floating-point value using invariant or current culture.
    /// </summary>
    /// <param name="value">Payload text to parse.</param>
    /// <param name="result">Parsed payload value when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the value can be parsed.</returns>
    private static bool TryParse(string value, out float result)
    {
        var trimmed = value.Trim();
        if (float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out result))
            return true;
        if (float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out result))
            return true;
        return false;
    }
}
