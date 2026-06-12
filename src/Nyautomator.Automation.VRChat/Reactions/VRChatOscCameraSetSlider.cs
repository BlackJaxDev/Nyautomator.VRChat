using System.Globalization;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that sets a VRChat user camera slider value over OSC.
/// </summary>
public class VRChatOscCameraSetSlider : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the camera slider setter.
    /// </summary>
    public override string Id => "VRChat.OSC.Camera.SetSlider";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat Camera: Set Slider";

    /// <summary>
    /// Gets the user-facing explanation of what camera state this reaction changes.
    /// </summary>
    public override string Description => "Sets a numeric slider value on the VRChat user camera.";

    /// <summary>
    /// Gets or sets the named camera slider to update.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscCameraSliderOptionsProvider))]
    public string Slider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the slider value text; blank or invalid values fall back to the slider default when known.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Parses the configured value and sends it, or sends the slider's default when parsing fails and metadata is known.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Slider))
            return;

        if (!string.IsNullOrWhiteSpace(Value))
        {
            if (TryParse(Value, out var parsed))
            {
                VRChatHelper.OSC.SetCameraSlider(Slider, parsed);
                return;
            }
        }

        if (VRChatHelper.OSC.TryGetCameraSlider(Slider, out var definition))
            VRChatHelper.OSC.SetCameraSlider(Slider, definition.Default);
    }

    /// <summary>
    /// Parses slider value text as a floating-point value using invariant or current culture.
    /// </summary>
    /// <param name="value">Slider value text to parse.</param>
    /// <param name="result">Parsed slider value when parsing succeeds.</param>
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
