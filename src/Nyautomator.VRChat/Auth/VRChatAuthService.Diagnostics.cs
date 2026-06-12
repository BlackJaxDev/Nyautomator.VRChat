using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;

namespace Nyautomator;

/// <summary>
/// Diagnostic string and redaction helpers for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Builds a login diagnostic that includes safe status details and redacted raw response content.
    /// </summary>
    /// <param name="response">Current-user response received during login.</param>
    /// <returns>Diagnostic text suitable for logs.</returns>
    private string BuildLoginDiagnostic(ApiResponse<CurrentUser> response)
    {
        var rawSummary = string.IsNullOrWhiteSpace(response.RawContent)
            ? "(empty)"
            : TruncateForLog(RedactSensitiveText(response.RawContent), 500);

        return $"{BuildLoginSafeDiagnostic(response)}; Raw={rawSummary}";
    }

    /// <summary>
    /// Builds a safe login diagnostic that avoids raw response bodies.
    /// </summary>
    /// <param name="response">Current-user response received during login.</param>
    /// <returns>Safe diagnostic text containing status, auth cookie presence, and verification methods.</returns>
    private string BuildLoginSafeDiagnostic(ApiResponse<CurrentUser> response)
    {
        var methods = _twoFactorMethods.Count == 0 ? "none" : string.Join(", ", _twoFactorMethods);
        var hasAuth = HasPendingAuthCookie() ? "yes" : "no";
        return $"status {(int)response.StatusCode}; auth cookie: {hasAuth}; verification methods: {methods}";
    }

    /// <summary>
    /// Redacts sensitive properties from JSON text, returning the original text when it is not valid JSON.
    /// </summary>
    /// <param name="value">Raw diagnostic text to redact.</param>
    /// <returns>Redacted JSON text or the original input.</returns>
    private static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        try
        {
            using var document = JsonDocument.Parse(value);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
                WriteRedactedJson(document.RootElement, writer);
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return value;
        }
    }

    /// <summary>
    /// Recursively writes JSON while replacing sensitive property values with a redaction marker.
    /// </summary>
    /// <param name="element">JSON element to write.</param>
    /// <param name="writer">Writer receiving redacted JSON.</param>
    private static void WriteRedactedJson(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsSensitiveJsonProperty(property.Name))
                        writer.WriteStringValue("[redacted]");
                    else
                        WriteRedactedJson(property.Value, writer);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteRedactedJson(item, writer);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                element.WriteTo(writer);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                writer.WriteNullValue();
                break;
        }
    }

    /// <summary>
    /// Determines whether a JSON property name is sensitive enough to redact in diagnostics.
    /// </summary>
    /// <param name="propertyName">Property name to inspect.</param>
    /// <returns><see langword="true"/> when the property name suggests token, cookie, password, or auth content.</returns>
    private static bool IsSensitiveJsonProperty(string propertyName)
        => propertyName.Contains("token", StringComparison.OrdinalIgnoreCase)
           || propertyName.Contains("cookie", StringComparison.OrdinalIgnoreCase)
           || propertyName.Contains("password", StringComparison.OrdinalIgnoreCase)
           || propertyName.Contains("auth", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Truncates long diagnostic strings to a maximum length with an ellipsis suffix.
    /// </summary>
    /// <param name="value">Text to truncate.</param>
    /// <param name="maxLength">Maximum number of characters before the ellipsis is appended.</param>
    /// <returns>The original text or a truncated version.</returns>
    private static string TruncateForLog(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "...";
}
