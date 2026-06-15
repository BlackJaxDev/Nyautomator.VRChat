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
/// Raw authenticated VRChat API forwarding for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Sends an authenticated raw HTTP request to the configured VRChat API host using stored session cookies.
    /// </summary>
    /// <param name="method">HTTP method to use; unsupported values fall back to GET.</param>
    /// <param name="path">Relative VRChat API path or absolute URL on the configured host.</param>
    /// <param name="query">Query string or JSON object converted into query parameters.</param>
    /// <param name="body">Optional body object for methods that support request content.</param>
    /// <param name="contentType">Request content type, defaulting to application/json.</param>
    /// <param name="timeoutMs">Per-request timeout in milliseconds, or non-positive for caller-controlled timeout.</param>
    /// <param name="cancellationToken">Token that cancels the request.</param>
    /// <returns>A raw API response containing status, body, headers, and error text.</returns>
    public async Task<VRChatRawApiResponse> SendAuthenticatedRequestAsync(
        string? method,
        string? path,
        string? query,
        object? body,
        string? contentType,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        string? authCookie;
        string? twoFactorCookie;

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var status = BuildStatus();
            if (!status.IsConnected)
            {
                return new VRChatRawApiResponse(
                    Success: false,
                    IsSuccess: false,
                    StatusCode: 0,
                    Body: string.Empty,
                    Headers: string.Empty,
                    Error: status.LastError ?? "VRChat is not authenticated.");
            }

            authCookie = GetMetadata(MetadataAuthCookie);
            twoFactorCookie = GetMetadata(MetadataTwoFactorCookie);
        }
        finally
        {
            _sync.Release();
        }

        var requestUri = BuildRequestUri(DefaultBasePath, path, query);
        var requestKey = $"{NormalizeMethod(method)} {requestUri.AbsolutePath}";
        var attempt = 0;
        var backoff = RateLimitInitialDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            using var cts = timeoutMs > 0
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                : null;
            if (cts is not null)
                cts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs)));

            var effectiveToken = cts?.Token ?? cancellationToken;

            try
            {
                using var client = new HttpClient
                {
                    Timeout = Timeout.InfiniteTimeSpan
                };
                using var request = new HttpRequestMessage(new HttpMethod(NormalizeMethod(method)), requestUri);
                request.Headers.UserAgent.TryParseAdd(DefaultUserAgent);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                ApplyCookieHeader(request, authCookie, twoFactorCookie);

                if (RequestSupportsBody(request.Method) && body is not null)
                    request.Content = BuildHttpContent(body, contentType);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, effectiveToken).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(effectiveToken).ConfigureAwait(false);
                var headers = FormatHeaders(response);

                if (ShouldRetry(response.StatusCode, attempt))
                {
                    attempt++;
                    var wait = GetRetryDelay(response, backoff);
                    backoff = NextBackoff(backoff);
                    EmitWarning("RateLimited", $"VRChat raw API request transient response ({(int)response.StatusCode}). Retry {attempt}/{RateLimitMaxAttempts} in {wait.TotalSeconds:F1}s.");
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if ((int)response.StatusCode is 403 or 404)
                    _failedRequests[requestKey] = DateTime.UtcNow;
                else
                    _failedRequests.Remove(requestKey);

                return new VRChatRawApiResponse(
                    Success: true,
                    IsSuccess: response.IsSuccessStatusCode,
                    StatusCode: (int)response.StatusCode,
                    Body: responseBody,
                    Headers: headers,
                    Error: response.IsSuccessStatusCode ? null : $"VRChat API request failed: {(int)response.StatusCode} {response.StatusCode}.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new VRChatRawApiResponse(false, false, 0, string.Empty, string.Empty, "VRChat API request timed out.");
            }
            catch (Exception ex)
            {
                return new VRChatRawApiResponse(false, false, 0, string.Empty, string.Empty, ex.Message);
            }
        }
    }

    /// <summary>
    /// Builds a VRChat API URI from base path, relative or same-host absolute path, and optional query input.
    /// </summary>
    /// <param name="basePath">Configured VRChat API base path.</param>
    /// <param name="path">Relative API path or same-host absolute URL.</param>
    /// <param name="query">Additional query string or JSON object.</param>
    /// <returns>The full request URI.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an absolute URL targets a different host.</exception>
    private static Uri BuildRequestUri(string basePath, string? path, string? query)
    {
        var normalizedBase = string.IsNullOrWhiteSpace(basePath) ? DefaultBasePath : basePath.Trim();
        var baseUri = new Uri(normalizedBase.TrimEnd('/') + "/");
        var relative = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (Uri.TryCreate(relative, UriKind.Absolute, out var absolute))
        {
            if (!string.Equals(absolute.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("VRChat authenticated requests must target the configured VRChat API host.");

            relative = absolute.PathAndQuery;
        }

        var split = relative.Split('?', 2);
        var pathPart = split[0].TrimStart('/');
        var queryPart = split.Length > 1 ? split[1] : string.Empty;
        var extraQuery = BuildQueryString(query);
        var builder = new UriBuilder(new Uri(baseUri, pathPart))
        {
            Query = CombineQuery(queryPart, extraQuery)
        };
        return builder.Uri;
    }

    /// <summary>
    /// Normalizes a query argument, including JSON-object input, into URL query string form.
    /// </summary>
    /// <param name="query">Raw query string or JSON object.</param>
    /// <returns>A query string without a leading question mark.</returns>
    private static string BuildQueryString(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var trimmed = query.Trim().TrimStart('?');
        if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            return trimmed;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return string.Empty;

            var pairs = new List<string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => property.Value.GetRawText()
                };
                if (value is null)
                    continue;

                pairs.Add($"{Uri.EscapeDataString(property.Name)}={Uri.EscapeDataString(value)}");
            }
            return string.Join("&", pairs);
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>
    /// Combines two query strings while preserving existing parameters.
    /// </summary>
    /// <param name="left">Existing query string.</param>
    /// <param name="right">Additional query string.</param>
    /// <returns>The combined query string without a leading question mark.</returns>
    private static string CombineQuery(string? left, string? right)
    {
        left = (left ?? string.Empty).Trim().TrimStart('?');
        right = (right ?? string.Empty).Trim().TrimStart('?');
        if (left.Length == 0)
            return right;
        if (right.Length == 0)
            return left;
        return $"{left}&{right}";
    }

    /// <summary>
    /// Normalizes an HTTP method name and falls back to GET for unsupported values.
    /// </summary>
    /// <param name="method">Raw method name.</param>
    /// <returns>A supported uppercase method name.</returns>
    private static string NormalizeMethod(string? method)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        return normalized is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS"
            ? normalized
            : "GET";
    }

    /// <summary>
    /// Checks whether a method is allowed to carry request content.
    /// </summary>
    /// <param name="method">HTTP method to inspect.</param>
    /// <returns><see langword="true"/> when content may be attached.</returns>
    private static bool RequestSupportsBody(HttpMethod method)
        => method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Delete;

    /// <summary>
    /// Serializes raw body content into an HTTP content object.
    /// </summary>
    /// <param name="body">String, JSON element, or object body to send.</param>
    /// <param name="contentType">Content type to apply, defaulting to application/json.</param>
    /// <returns>HTTP content ready to attach to a request.</returns>
    private static HttpContent BuildHttpContent(object body, string? contentType)
    {
        var mediaType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType.Trim();
        var text = body switch
        {
            string s => s,
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(body)
        };
        return new StringContent(text, Encoding.UTF8, mediaType);
    }

    /// <summary>
    /// Adds stored VRChat auth cookies to a raw HTTP request header.
    /// </summary>
    /// <param name="request">Request message to update.</param>
    /// <param name="authCookie">Stored auth cookie value.</param>
    /// <param name="twoFactorCookie">Stored two-factor cookie value.</param>
    private static void ApplyCookieHeader(HttpRequestMessage request, string? authCookie, string? twoFactorCookie)
    {
        var cookies = new List<string>();
        if (!string.IsNullOrWhiteSpace(authCookie))
            cookies.Add($"auth={authCookie}");
        if (!string.IsNullOrWhiteSpace(twoFactorCookie))
            cookies.Add($"twoFactorAuth={twoFactorCookie}");
        if (cookies.Count > 0)
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", cookies));
    }

    /// <summary>
    /// Flattens response and content headers into CRLF-separated text.
    /// </summary>
    /// <param name="response">HTTP response whose headers should be formatted.</param>
    /// <returns>Formatted response header text.</returns>
    private static string FormatHeaders(HttpResponseMessage response)
    {
        var builder = new StringBuilder();
        foreach (var header in response.Headers)
        {
            foreach (var value in header.Value)
                builder.Append(header.Key).Append(": ").AppendLine(value);
        }

        foreach (var header in response.Content.Headers)
        {
            foreach (var value in header.Value)
                builder.Append(header.Key).Append(": ").AppendLine(value);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Determines whether a raw HTTP response should be retried for rate-limit or server-error status codes.
    /// </summary>
    /// <param name="statusCode">HTTP status code returned by VRChat.</param>
    /// <param name="attempt">Zero-based retry attempt number.</param>
    /// <returns><see langword="true"/> when another retry should be attempted.</returns>
    private static bool ShouldRetry(HttpStatusCode statusCode, int attempt)
        => attempt < RateLimitMaxAttempts && (statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500);

    /// <summary>
    /// Computes retry delay for raw HTTP responses, respecting Retry-After when it is longer than current backoff.
    /// </summary>
    /// <param name="response">HTTP response returned by VRChat.</param>
    /// <param name="current">Current exponential backoff value.</param>
    /// <returns>Delay to wait before retrying.</returns>
    private static TimeSpan GetRetryDelay(HttpResponseMessage response, TimeSpan current)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is not null)
        {
            TimeSpan? parsed = retryAfter.Delta;
            if (!parsed.HasValue && retryAfter.Date.HasValue)
                parsed = retryAfter.Date.Value - DateTimeOffset.UtcNow;

            if (parsed.HasValue && parsed.Value > TimeSpan.Zero)
            {
                var capped = parsed.Value > RateLimitMaxDelay ? RateLimitMaxDelay : parsed.Value;
                if (capped > current)
                    return capped;
            }
        }

        return current > RateLimitMaxDelay ? RateLimitMaxDelay : current;
    }
}
