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
/// Converts low-level VRChat SDK API exceptions into user-facing error text and diagnostic detail.
/// </summary>
internal static class VRChatErrorTranslator
{
    /// <summary>
    /// Produces a friendly error message from a VRChat SDK exception.
    /// </summary>
    /// <param name="ex">SDK exception to translate.</param>
    /// <returns>User-facing error text.</returns>
    public static string Translate(ApiException ex)
    {
        var normalized = BuildNormalized(ex);

        return ex.ErrorCode switch
        {
            400 when Contains(normalized, "invalid") && Contains(normalized, "credential")
                => "VRChat rejected the username or password provided.",
            400 when Contains(normalized, "two") && Contains(normalized, "factor") && Contains(normalized, "code")
                => "VRChat requires a valid two-factor code before continuing.",
            401 or 403 when Contains(normalized, "email") && Contains(normalized, "otp")
                => "VRChat requires the email verification code that was sent to you.",
            401 or 403
                => "The VRChat session is no longer valid.",
            429
                => "VRChat rate limited the request. Please try again shortly.",
            500 or 502 or 503 or 504
                => "VRChat services appear to be unavailable right now. Try again soon.",
            _ when Contains(normalized, "timeout")
                => "The VRChat request timed out. Check your network connection and try again.",
            _ when Contains(normalized, "name resolution") || Contains(normalized, "dns")
                => "VRChat endpoint could not be resolved. Verify your internet connection.",
            _ when Contains(normalized, "ssl") && Contains(normalized, "handshake")
                => "Could not establish a secure connection to VRChat.",
            _ when Contains(normalized, "maintenance")
                => "VRChat is undergoing maintenance. Please retry later.",
            _ when Contains(normalized, "banned") && Contains(normalized, "ip")
                => "VRChat temporarily blocked requests from this IP. Wait and try again.",
            _ => string.IsNullOrWhiteSpace(ex.Message)
                ? "An unexpected VRChat error occurred."
                : ex.Message!
        };
    }

    /// <summary>
    /// Builds diagnostic detail from an exception status code and extracted response content.
    /// </summary>
    /// <param name="ex">SDK exception to inspect.</param>
    /// <returns>Detail text suitable for logs.</returns>
    public static string BuildDetail(ApiException ex)
    {
        var content = ExtractErrorContent(ex);
        if (string.IsNullOrWhiteSpace(content))
            return ex.ErrorCode > 0 ? $"Status {ex.ErrorCode}" : "No additional detail provided.";

        var detail = content.Trim();
        return ex.ErrorCode > 0 ? $"Status {ex.ErrorCode}: {detail}" : detail;
    }

    /// <summary>
    /// Builds a lowercase search string from exception message and response content.
    /// </summary>
    /// <param name="ex">SDK exception to normalize.</param>
    /// <returns>Normalized text used by translation rules.</returns>
    private static string BuildNormalized(ApiException ex)
    {
        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(ex.Message))
            builder.Append(ex.Message).Append(' ');

        var content = ExtractErrorContent(ex);
        if (!string.IsNullOrWhiteSpace(content))
            builder.Append(content);

        return builder.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Extracts response content from SDK exception properties exposed by different generated client versions.
    /// </summary>
    /// <param name="ex">SDK exception to inspect.</param>
    /// <returns>Response content text, or <see langword="null"/> when unavailable.</returns>
    private static string? ExtractErrorContent(ApiException ex)
    {
        if (ex == null)
            return null;

        string? content = null;

        try
        {
            content = ex.GetType().GetProperty("ErrorContent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(ex) as string;
        }
        catch { }

        if (string.IsNullOrWhiteSpace(content))
        {
            try
            {
                content = ex.GetType().GetProperty("Content", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(ex) as string;
            }
            catch { }
        }

        return string.IsNullOrWhiteSpace(content) ? null : content;
    }

    /// <summary>
    /// Performs a case-insensitive substring check against normalized exception text.
    /// </summary>
    /// <param name="normalizedSource">Normalized text to search.</param>
    /// <param name="value">Substring to find.</param>
    /// <returns><see langword="true"/> when the value appears in the source.</returns>
    private static bool Contains(string normalizedSource, string value)
        => normalizedSource.Contains(value, StringComparison.OrdinalIgnoreCase);
}
