using System;
using System.ComponentModel;
using System.Globalization;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that sends values to VRChat's built-in OSC input endpoints.
/// </summary>
public class VRChatOscSendInput : ReactionType
{
    /// <summary>
    /// Gets the automation registry id for the OSC input sender.
    /// </summary>
    public override string Id => "VRChat.OSC.SendInput";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Send OSC Input";

    /// <summary>
    /// Gets the user-facing explanation of how the reaction targets VRChat input endpoints.
    /// </summary>
    public override string Description => "Sends a value to VRChat's built-in OSC input endpoints (Jump, MoveForward, etc).";

    /// <summary>
    /// Gets or sets the VRChat OSC input endpoint to send to.
    /// </summary>
    [OptionsProvider(typeof(VRChatOscInputEndpointOptionsProvider))]
    [Description("Choose the VRChat OSC input endpoint. Buttons send 0/1; axes use the listed range.")]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional value override parsed and clamped against the endpoint definition.
    /// </summary>
    [Description("Override the value to send. Leave blank for default; buttons treat >= 0.5 as pressed, and axes clamp to their range.")]
    public string Value { get; set; } = "1";

    /// <summary>
    /// Resolves the configured endpoint and sends a normalized float value over OSC.
    /// </summary>
    public override void Execute()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
            return;

        var key = Endpoint.Trim();

        if (VRChatHelper.OSC.TryGetInputEndpoint(key, out var endpoint))
        {
            var resolved = ResolveValue(Value, endpoint);
            VRChatHelper.OSC.SendInput(key, resolved);
            return;
        }

        if (TryParseFloat(Value, out var fallback))
            VRChatHelper.OSC.SendInput(key, fallback);
    }

    /// <summary>
    /// Parses an input value, applies endpoint defaults, clamps to the endpoint range, and normalizes buttons to 0 or 1.
    /// </summary>
    /// <param name="rawValue">Raw value text from the reaction property.</param>
    /// <param name="endpoint">Endpoint definition that supplies default value, range, and button semantics.</param>
    /// <returns>The normalized value to send to VRChat.</returns>
    private static float ResolveValue(string? rawValue, in VRChatHelper.OSC.OscInputEndpointDefinition endpoint)
    {
        var trimmed = rawValue?.Trim();
        float resolved;

        if (string.IsNullOrEmpty(trimmed))
        {
            resolved = endpoint.IsButton ? 1f : endpoint.Default;
        }
        else if (bool.TryParse(trimmed, out var boolValue))
        {
            resolved = boolValue ? 1f : 0f;
        }
        else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) ||
                 int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue))
        {
            resolved = intValue;
        }
        else if (float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue) ||
                 float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out floatValue))
        {
            resolved = floatValue;
        }
        else
        {
            resolved = endpoint.Default;
        }

        resolved = Math.Clamp(resolved, endpoint.Min, endpoint.Max);
        if (endpoint.IsButton)
            resolved = resolved >= 0.5f ? 1f : 0f;

        return resolved;
    }

    /// <summary>
    /// Parses a fallback endpoint value when no endpoint metadata is available.
    /// </summary>
    /// <param name="value">Raw value text from the reaction property.</param>
    /// <param name="result">Parsed float value when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the value can be interpreted as bool, float, or integer.</returns>
    private static bool TryParseFloat(string? value, out float result)
    {
        result = 0f;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        if (bool.TryParse(trimmed, out var boolValue))
        {
            result = boolValue ? 1f : 0f;
            return true;
        }

        if (float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue) ||
            float.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out floatValue))
        {
            result = floatValue;
            return true;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue) ||
            int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out intValue))
        {
            result = intValue;
            return true;
        }

        return false;
    }
}
