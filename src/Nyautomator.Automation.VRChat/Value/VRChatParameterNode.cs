using System;
using System.ComponentModel;
using System.Globalization;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Value node that reads the latest cached VRChat OSC avatar parameter value and related metadata.
/// </summary>
[AutomationOutputs(
    "out",
    "value",
    "float",
    "int",
    "bool",
    "exists",
    "hasvalue",
    "usedefault",
    "type",
    "name",
    "source",
    "error"
)]
public sealed class VRChatParameterNode : ValueNodeType
{
    /// <summary>
    /// Gets the automation registry id for the VRChat parameter value node.
    /// </summary>
    public override string Id => "Value.VRChat.Parameter";

    /// <summary>
    /// Gets the label shown for this value node in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Parameter";

    /// <summary>
    /// Gets the user-facing explanation of the parameter value outputs.
    /// </summary>
    public override string Description => "Reads VRChat OSC parameter values (float, int, or bool) with optional defaults when values are unavailable.";

    /// <summary>
    /// Gets or sets the VRChat OSC avatar parameter name to resolve and read.
    /// </summary>
    [Description("Name of the VRChat OSC parameter to read (case-insensitive by default).")]
    public string? Parameter { get; set; }

    /// <summary>
    /// Gets or sets the expected parameter type, or auto-detection from live values and avatar metadata.
    /// </summary>
    [Description("Expected parameter type. Set to Auto to infer from live data or avatar metadata.")]
    public VRChatParameterKind ParameterKind { get; set; } = VRChatParameterKind.Auto;

    /// <summary>
    /// Gets or sets whether the configured parameter name is matched against cached names without case sensitivity.
    /// </summary>
    [Description("Treat the parameter name as case-insensitive when resolving against the avatar configuration.")]
    public bool IgnoreCase { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an empty parameter cache should trigger a throttled avatar config load attempt.
    /// </summary>
    [Description("Automatically reload the latest OSC avatar configuration when no parameters are cached yet.")]
    public bool AttemptAutoReloadConfig { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum number of seconds between automatic avatar config load attempts.
    /// </summary>
    [Description("Minimum seconds between automatic config reload attempts.")]
    public double ConfigReloadCooldownSeconds { get; set; } = 15d;

    /// <summary>
    /// Gets or sets the fallback value converted to the resolved parameter kind when no live value is available.
    /// </summary>
    [Description("Fallback value when the parameter is missing or has not been populated yet.")]
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Synchronizes automatic avatar config reload throttling across all parameter nodes.
    /// </summary>
    private static readonly object ConfigReloadSync = new();

    /// <summary>
    /// Stores the last UTC time an automatic avatar config reload was attempted.
    /// </summary>
    private static DateTimeOffset _lastReloadAttempt = DateTimeOffset.MinValue;

    /// <summary>
    /// Reads the configured parameter, builds a typed snapshot, and returns the requested output handle.
    /// </summary>
    /// <param name="outputHandle">Value output handle requested by the automation graph.</param>
    /// <returns>The value or metadata associated with the requested output handle.</returns>
    public override object? ComputeValue(string? outputHandle)
    {
        MaybeReloadConfig();

        var handle = NormalizeHandle(outputHandle);
        var parameterName = ResolveParameterName(Parameter);
        if (string.IsNullOrEmpty(parameterName))
        {
            var missing = new VRChatParameterSnapshot(string.Empty, VRChatParameterKind.Auto, false, false, false, DefaultValue, null, null, null, "Parameter name is required.");
            return SelectOutput(handle, missing);
        }

        var boolAvailable = VRChatHelper.OSC.TryGetBoolParameter(parameterName, out var boolValue);
        var intAvailable = VRChatHelper.OSC.TryGetIntParameter(parameterName, out var intValue);
        var floatAvailable = VRChatHelper.OSC.TryGetFloatParameter(parameterName, out var floatValue);

        var exists = boolAvailable || intAvailable || floatAvailable || ParameterExists(parameterName);
        var resolvedKind = ResolveKind(ParameterKind, parameterName, boolAvailable, intAvailable, floatAvailable);

        var snapshot = BuildSnapshot(parameterName, resolvedKind, exists, boolAvailable ? boolValue : (bool?)null, intAvailable ? intValue : (int?)null, floatAvailable ? floatValue : (float?)null);
        return SelectOutput(handle, snapshot);
    }

    /// <summary>
    /// Resolves the .NET type exposed by each supported parameter output handle.
    /// </summary>
    /// <param name="outputHandle">Value output handle requested by the automation graph.</param>
    /// <returns>The .NET type expected for that output handle.</returns>
    public override Type? GetOutputType(string? outputHandle)
    {
        var handle = NormalizeHandle(outputHandle);
        return ResolveOutputTypeToken(handle switch
        {
            "" or "out" or "value" => null,
            "float" => "float64",
            "int" => "int32",
            "bool" => "boolean",
            "exists" or "hasvalue" or "usedefault" => "boolean",
            "type" or "name" or "source" or "error" => "string",
            _ => null
        }, fallbackToObject: false);
    }

    /// <summary>
    /// Combines live parameter values, resolved kind, existence metadata, and default conversion into a snapshot.
    /// </summary>
    /// <param name="parameterName">Resolved VRChat parameter name.</param>
    /// <param name="kind">Resolved parameter kind to expose.</param>
    /// <param name="exists">Whether the parameter exists in live values or cached avatar metadata.</param>
    /// <param name="boolValue">Live boolean value when available.</param>
    /// <param name="intValue">Live integer value when available.</param>
    /// <param name="floatValue">Live floating-point value when available.</param>
    /// <returns>A snapshot containing typed outputs and any conversion or existence error.</returns>
    private VRChatParameterSnapshot BuildSnapshot(string parameterName, VRChatParameterKind kind, bool exists, bool? boolValue, int? intValue, float? floatValue)
    {
        object? value = null;
        double? floatResult = null;
        int? intResult = null;
        bool? boolResult = null;
        bool usedDefault = false;
        bool hasValue = false;
        string? error = null;

        switch (kind)
        {
            case VRChatParameterKind.Bool:
                if (boolValue.HasValue)
                {
                    boolResult = boolValue.Value;
                    hasValue = true;
                }
                else if (DefaultValue is not null && TryConvertDefaultToBool(DefaultValue, out var defaultBool))
                {
                    boolResult = defaultBool;
                    hasValue = true;
                    usedDefault = true;
                }
                else if (DefaultValue is not null)
                {
                    error = AppendError(error, "DefaultValue could not be converted to a boolean.");
                }
                value = boolResult ?? DefaultValue;
                break;

            case VRChatParameterKind.Int:
                if (intValue.HasValue)
                {
                    intResult = intValue.Value;
                    hasValue = true;
                }
                else if (DefaultValue is not null && TryConvertDefaultToInt(DefaultValue, out var defaultInt))
                {
                    intResult = defaultInt;
                    hasValue = true;
                    usedDefault = true;
                }
                else if (DefaultValue is not null)
                {
                    error = AppendError(error, "DefaultValue could not be converted to an integer.");
                }
                value = intResult ?? DefaultValue;
                break;

            default:
                if (floatValue.HasValue)
                {
                    floatResult = Math.Round(floatValue.Value, 6);
                    hasValue = true;
                }
                else if (DefaultValue is not null && TryConvertDefaultToDouble(DefaultValue, out var defaultFloat))
                {
                    floatResult = defaultFloat;
                    hasValue = true;
                    usedDefault = true;
                }
                else if (DefaultValue is not null)
                {
                    error = AppendError(error, "DefaultValue could not be converted to a floating-point value.");
                }
                value = floatResult ?? DefaultValue;
                break;
        }

        if (!exists)
            error = AppendError(error, $"VRChat parameter '{parameterName}' was not found.");

        return new VRChatParameterSnapshot(
            parameterName,
            kind,
            exists,
            hasValue,
            usedDefault,
            value,
            floatResult,
            intResult,
            boolResult,
            error);
    }

    /// <summary>
    /// Selects the requested automation output from a parameter snapshot.
    /// </summary>
    /// <param name="handle">Normalized output handle.</param>
    /// <param name="snapshot">Snapshot produced for the current parameter read.</param>
    /// <returns>The snapshot field associated with the requested handle.</returns>
    private static object? SelectOutput(string handle, VRChatParameterSnapshot snapshot)
    {
        return handle switch
        {
            "" or "out" or "value" => snapshot.Value,
            "float" => snapshot.FloatValue,
            "int" => snapshot.IntValue,
            "bool" => snapshot.BoolValue,
            "exists" => snapshot.Exists,
            "hasvalue" => snapshot.HasValue,
            "usedefault" => snapshot.UsedDefault,
            "type" or "kind" => snapshot.Kind.ToString().ToLowerInvariant(),
            "name" => snapshot.ParameterName,
            "source" => snapshot.UsedDefault ? "default" : snapshot.HasValue ? "value" : "missing",
            "error" => snapshot.Error,
            _ => snapshot.Value
        };
    }

    /// <summary>
    /// Normalizes an output handle using the automation helper and lowercases it for switch matching.
    /// </summary>
    /// <param name="handle">Raw output handle supplied by the automation graph.</param>
    /// <returns>A normalized lowercase handle, or an empty string for the default output.</returns>
    private static string NormalizeHandle(string? handle)
    {
        var normalized = AutomationValueHelper.NormalizeValueHandle(handle);
        return string.IsNullOrEmpty(normalized) ? string.Empty : normalized.ToLowerInvariant();
    }

    /// <summary>
    /// Attempts to load the latest avatar OSC config when no cached parameter names exist and the cooldown permits.
    /// </summary>
    private void MaybeReloadConfig()
    {
        if (!AttemptAutoReloadConfig)
            return;

        if (VRChatHelper.OSC.ParameterNames.Count > 0)
            return;

        var now = DateTimeOffset.UtcNow;
        var cooldown = TimeSpan.FromSeconds(Math.Max(1d, ConfigReloadCooldownSeconds));

        lock (ConfigReloadSync)
        {
            if (now - _lastReloadAttempt < cooldown)
                return;

            _lastReloadAttempt = now;
        }

        try
        {
            VRChatHelper.OSC.TryLoadLatestAvatarConfig();
        }
        catch
        {
            // Ignore reload failures; consumers can retry later.
        }
    }

    /// <summary>
    /// Resolves the configured parameter name against cached names and metadata, honoring the case-insensitive setting.
    /// </summary>
    /// <param name="parameter">Raw parameter name from the node property.</param>
    /// <returns>The canonical cached parameter name when found, otherwise the trimmed configured name.</returns>
    private string? ResolveParameterName(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter))
            return null;

        var trimmed = parameter.Trim();
        if (!IgnoreCase)
            return trimmed;

        foreach (var name in VRChatHelper.OSC.ParameterNames)
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        foreach (var entry in VRChatHelper.OSC.CurrentParameters)
        {
            if (string.Equals(entry.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                return entry.Name;
        }

        return trimmed;
    }

    /// <summary>
    /// Checks whether a parameter appears in cached OSC parameter names or the current avatar parameter metadata.
    /// </summary>
    /// <param name="parameterName">Resolved parameter name to check.</param>
    /// <returns><see langword="true"/> when the parameter is known in either cache.</returns>
    private static bool ParameterExists(string parameterName)
    {
        foreach (var name in VRChatHelper.OSC.ParameterNames)
        {
            if (string.Equals(name, parameterName, StringComparison.Ordinal))
                return true;
        }

        foreach (var entry in VRChatHelper.OSC.CurrentParameters)
        {
            if (string.Equals(entry.Name, parameterName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines the effective parameter kind from the configured kind, live value availability, or avatar metadata.
    /// </summary>
    /// <param name="requested">Kind configured on the node.</param>
    /// <param name="parameterName">Resolved parameter name.</param>
    /// <param name="hasBool">Whether a live boolean value exists.</param>
    /// <param name="hasInt">Whether a live integer value exists.</param>
    /// <param name="hasFloat">Whether a live floating-point value exists.</param>
    /// <returns>The kind used to build the output snapshot.</returns>
    private static VRChatParameterKind ResolveKind(VRChatParameterKind requested, string parameterName, bool hasBool, bool hasInt, bool hasFloat)
    {
        if (requested != VRChatParameterKind.Auto)
            return requested;

        if (hasBool)
            return VRChatParameterKind.Bool;
        if (hasInt)
            return VRChatParameterKind.Int;
        if (hasFloat)
            return VRChatParameterKind.Float;

        var inferred = InferKindFromMetadata(parameterName);
        return inferred ?? VRChatParameterKind.Float;
    }

    /// <summary>
    /// Infers a parameter kind from the current avatar parameter metadata type string.
    /// </summary>
    /// <param name="parameterName">Resolved parameter name to search for.</param>
    /// <returns>The mapped parameter kind, or <see langword="null"/> when metadata is missing or unknown.</returns>
    private static VRChatParameterKind? InferKindFromMetadata(string parameterName)
    {
        foreach (var entry in VRChatHelper.OSC.CurrentParameters)
        {
            if (!string.Equals(entry.Name, parameterName, StringComparison.Ordinal))
                continue;

            return MapKind(entry.Type);
        }

        return null;
    }

    /// <summary>
    /// Maps VRChat avatar metadata type names to automation parameter kinds.
    /// </summary>
    /// <param name="type">Raw type string from avatar metadata.</param>
    /// <returns>The mapped kind, or <see langword="null"/> for blank or unknown type names.</returns>
    private static VRChatParameterKind? MapKind(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return null;

        return type.Trim().ToLowerInvariant() switch
        {
            "bool" or "boolean" => VRChatParameterKind.Bool,
            "int" or "integer" => VRChatParameterKind.Int,
            "float" or "double" or "single" => VRChatParameterKind.Float,
            _ => null
        };
    }

    /// <summary>
    /// Converts a default value to a boolean using direct bool, bool text, or automation type conversion.
    /// </summary>
    /// <param name="value">Default value supplied to the node.</param>
    /// <param name="result">Converted boolean when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryConvertDefaultToBool(object value, out bool result)
    {
        result = false;
        try
        {
            switch (value)
            {
                case bool b:
                    result = b;
                    return true;
                case string s when bool.TryParse(s, out var parsed):
                    result = parsed;
                    return true;
                default:
                    if (AutomationTypeTokens.TryConvertToTarget(value, typeof(bool), out var converted) && converted is bool convertedBool)
                    {
                        result = convertedBool;
                        return true;
                    }
                    break;
            }
        }
        catch
        {
            // ignore conversion errors
        }

        return false;
    }

    /// <summary>
    /// Converts a default value to an integer using numeric narrowing, integer text, or automation type conversion.
    /// </summary>
    /// <param name="value">Default value supplied to the node.</param>
    /// <param name="result">Converted integer when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds without overflow.</returns>
    private static bool TryConvertDefaultToInt(object value, out int result)
    {
        result = 0;
        try
        {
            switch (value)
            {
                case int i:
                    result = i;
                    return true;
                case long l when l is >= int.MinValue and <= int.MaxValue:
                    result = (int)l;
                    return true;
                case double d when d >= int.MinValue && d <= int.MaxValue:
                    result = (int)Math.Round(d, MidpointRounding.AwayFromZero);
                    return true;
                case float f when f >= int.MinValue && f <= int.MaxValue:
                    result = (int)Math.Round(f, MidpointRounding.AwayFromZero);
                    return true;
                case decimal dec when dec >= int.MinValue && dec <= int.MaxValue:
                    result = (int)Math.Round(dec, MidpointRounding.AwayFromZero);
                    return true;
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;
                default:
                    if (AutomationTypeTokens.TryConvertToTarget(value, typeof(int), out var converted) && converted is int convertedInt)
                    {
                        result = convertedInt;
                        return true;
                    }
                    break;
            }
        }
        catch
        {
            // ignore conversion errors
        }

        return false;
    }

    /// <summary>
    /// Converts a default value to a double using numeric values, invariant float text, or automation type conversion.
    /// </summary>
    /// <param name="value">Default value supplied to the node.</param>
    /// <param name="result">Converted double when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryConvertDefaultToDouble(object value, out double result)
    {
        result = 0d;
        try
        {
            switch (value)
            {
                case double d:
                    result = d;
                    return true;
                case float f:
                    result = f;
                    return true;
                case decimal dec:
                    result = (double)dec;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                    result = parsed;
                    return true;
                default:
                    if (AutomationTypeTokens.TryConvertToTarget(value, typeof(double), out var converted) && converted is double convertedDouble)
                    {
                        result = convertedDouble;
                        return true;
                    }
                    break;
            }
        }
        catch
        {
            // ignore conversion errors
        }

        return false;
    }

    /// <summary>
    /// Appends a trimmed error message to an existing snapshot error string.
    /// </summary>
    /// <param name="existing">Existing error text, if any.</param>
    /// <param name="message">New error text to append.</param>
    /// <returns>The combined error string.</returns>
    private static string AppendError(string? existing, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return existing ?? string.Empty;

        if (string.IsNullOrWhiteSpace(existing))
            return message.Trim();

        return string.Concat(existing.Trim(), " ", message.Trim());
    }

    /// <summary>
    /// Immutable parameter read result used to service all output handles from one computation.
    /// </summary>
    /// <param name="ParameterName">Resolved parameter name used for the read.</param>
    /// <param name="Kind">Effective parameter kind selected for output conversion.</param>
    /// <param name="Exists">Whether the parameter exists in live values or cached metadata.</param>
    /// <param name="HasValue">Whether a live or converted default value is available.</param>
    /// <param name="UsedDefault">Whether the exposed value came from <see cref="DefaultValue"/>.</param>
    /// <param name="Value">Primary output value for the selected kind.</param>
    /// <param name="FloatValue">Floating-point output value when available.</param>
    /// <param name="IntValue">Integer output value when available.</param>
    /// <param name="BoolValue">Boolean output value when available.</param>
    /// <param name="Error">Conversion or missing-parameter error text.</param>
    private readonly record struct VRChatParameterSnapshot(
        string ParameterName,
        VRChatParameterKind Kind,
        bool Exists,
        bool HasValue,
        bool UsedDefault,
        object? Value,
        double? FloatValue,
        int? IntValue,
        bool? BoolValue,
        string? Error);
}

/// <summary>
/// Parameter kind used by <see cref="VRChatParameterNode"/> when selecting typed outputs.
/// </summary>
public enum VRChatParameterKind
{
    /// <summary>
    /// Infer the kind from live values first, then avatar metadata, then default to float.
    /// </summary>
    Auto,

    /// <summary>
    /// Treat the parameter as a floating-point value.
    /// </summary>
    Float,

    /// <summary>
    /// Treat the parameter as an integer value.
    /// </summary>
    Int,

    /// <summary>
    /// Treat the parameter as a boolean value.
    /// </summary>
    Bool
}
