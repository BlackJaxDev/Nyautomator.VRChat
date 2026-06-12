namespace NyautomatorUI.Server.Automation;

using Nyautomator;

/// <summary>
/// Reaction node that sends a bool, integer, or float avatar parameter value to VRChat over OSC.
/// </summary>
public class VRChatOscSetParameter : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the OSC parameter setter.
    /// </summary>
    public override string Id => "VRChat.OSC.SetParameter";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Set OSC Parameter";

    /// <summary>
    /// Gets the user-facing explanation of how the reaction sends parameter values.
    /// </summary>
    public override string Description => "Sends an OSC parameter update to VRChat. String values are allowed; runtime coerces to proper OSC type.";

    /// <summary>
    /// Gets or sets the VRChat OSC parameter name to update.
    /// </summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text value parsed as bool, int, or float before sending.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscParameterOptionsProvider))]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Sends the configured parameter when the value can be parsed to a VRChat-supported primitive type.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Parameter))
            return;

        var v = Value?.Trim() ?? string.Empty;
        if (bool.TryParse(v, out var b))
            VRChatHelper.OSC.SetParameter(Parameter, b);
        else if (int.TryParse(v, out var i))
            VRChatHelper.OSC.SetParameter(Parameter, i);
        else if (float.TryParse(v, out var f))
            VRChatHelper.OSC.SetParameter(Parameter, f);
        else
        {
            // Not a primitive type supported by VRChat OSC directly; ignore
        }
    }
}
