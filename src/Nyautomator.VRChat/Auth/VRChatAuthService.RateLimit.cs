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
/// Rate-limit and transient retry helpers for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Converts a VRChat SDK exception into auth service state and optionally clears expired sessions.
    /// </summary>
    /// <param name="ex">Exception returned by the generated VRChat SDK.</param>
    /// <param name="clearSessionOnUnauthorized">Whether unauthorized or forbidden responses should clear stored session state.</param>
    private void HandleApiException(ApiException ex, bool clearSessionOnUnauthorized)
    {
        var friendly = VRChatErrorTranslator.Translate(ex);
        var detail = VRChatErrorTranslator.BuildDetail(ex);

        if (clearSessionOnUnauthorized && (ex.ErrorCode == 401 || ex.ErrorCode == 403))
        {
            ResetState(clearMetadata: true);
            _sessionStore.Clear();
            _lastError = "VRChat session expired. Please log in again.";
            EmitWarning("SessionExpired", _lastError, detail);
            return;
        }

        _lastError = ex.ErrorCode > 0
            ? $"VRChat API error ({ex.ErrorCode}): {friendly}"
            : $"VRChat API error: {friendly}";
        EmitError("ApiError", _lastError, detail);
    }

    /// <summary>
    /// Executes a generated SDK operation through the shared limiter and retries transient failures.
    /// </summary>
    /// <typeparam name="T">SDK response payload type.</typeparam>
    /// <param name="operation">Operation that returns a VRChat SDK API response.</param>
    /// <param name="cancellationToken">Token that cancels waiting, retries, or the operation.</param>
    /// <param name="requestKey">Optional key used to suppress repeated recently failed requests.</param>
    /// <param name="retryRateLimit">Whether HTTP 429 responses should be retried by this helper.</param>
    /// <returns>The successful SDK API response.</returns>
    private async Task<ApiResponse<T>> ExecuteWithRateLimitRetryAsync<T>(Func<Task<ApiResponse<T>>> operation, CancellationToken cancellationToken, string? requestKey = null, bool retryRateLimit = true)
    {
        if (requestKey is not null && IsInFailedCooldown(requestKey))
            throw new ApiException(0, $"VRChat request '{requestKey}' is in a temporary cooldown after a recent 403/404 response.");

        var attempt = 0;
        var backoff = RateLimitInitialDelay;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var response = await operation().ConfigureAwait(false);
                if (attempt > 0)
                    _lastError = null;
                if (requestKey is not null)
                    _failedRequests.Remove(requestKey);
                return response;
            }
            catch (ApiException ex) when (IsTransientRetry(ex, attempt, retryRateLimit))
            {
                attempt++;
                var wait = GetRetryDelay(ex, backoff);
                backoff = NextBackoff(backoff);
                EmitWarning("RateLimited", $"VRChat transient error ({ex.ErrorCode}). Retry {attempt}/{RateLimitMaxAttempts} in {wait.TotalSeconds:F1}s.");
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                if (requestKey is not null && (ex.ErrorCode == 403 || ex.ErrorCode == 404))
                    _failedRequests[requestKey] = DateTime.UtcNow;
                throw;
            }
        }
    }

    /// <summary>
    /// Checks and expires the cooldown entry for a request that recently returned 403 or 404.
    /// </summary>
    /// <param name="requestKey">Request key to check.</param>
    /// <returns><see langword="true"/> when the request is still in cooldown.</returns>
    private bool IsInFailedCooldown(string requestKey)
    {
        if (_failedRequests.TryGetValue(requestKey, out var when))
        {
            if (DateTime.UtcNow - when < FailedRequestCooldown)
                return true;
            _failedRequests.Remove(requestKey);
        }

        return false;
    }

    /// <summary>
    /// Determines whether a VRChat SDK exception represents a transient retryable failure.
    /// </summary>
    /// <param name="ex">SDK exception to inspect.</param>
    /// <param name="attempt">Zero-based retry attempt number.</param>
    /// <param name="retryRateLimit">Whether HTTP 429 responses should be treated as retryable.</param>
    /// <returns><see langword="true"/> when the operation should be retried.</returns>
    private static bool IsTransientRetry(ApiException ex, int attempt, bool retryRateLimit = true)
        => attempt < RateLimitMaxAttempts
           && (((ex.ErrorCode == 429) && retryRateLimit) || (ex.ErrorCode >= 500 && ex.ErrorCode <= 599));

    /// <summary>
    /// Computes retry delay for SDK exceptions, respecting Retry-After when present and capped.
    /// </summary>
    /// <param name="ex">SDK exception whose headers may contain Retry-After.</param>
    /// <param name="current">Current exponential backoff value.</param>
    /// <returns>Delay to wait before retrying.</returns>
    private static TimeSpan GetRetryDelay(ApiException ex, TimeSpan current)
    {
        var retryAfter = TryParseRetryAfter(ex);
        if (retryAfter.HasValue)
        {
            var capped = retryAfter.Value > RateLimitMaxDelay ? RateLimitMaxDelay : retryAfter.Value;
            if (capped > current)
                return capped;
        }

        return current > RateLimitMaxDelay ? RateLimitMaxDelay : current;
    }

    /// <summary>
    /// Doubles the current retry backoff and caps it at the maximum delay.
    /// </summary>
    /// <param name="current">Current backoff value.</param>
    /// <returns>The next backoff value.</returns>
    private static TimeSpan NextBackoff(TimeSpan current)
    {
        var nextSeconds = Math.Min(current.TotalSeconds * 2, RateLimitMaxDelay.TotalSeconds);
        if (nextSeconds <= 0)
            nextSeconds = RateLimitInitialDelay.TotalSeconds;
        return TimeSpan.FromSeconds(nextSeconds);
    }

    /// <summary>
    /// Extracts a retry delay from a VRChat SDK exception's Retry-After headers.
    /// </summary>
    /// <param name="ex">SDK exception to inspect.</param>
    /// <returns>The parsed delay, or <see langword="null"/> when unavailable.</returns>
    private static TimeSpan? TryParseRetryAfter(ApiException ex)
    {
        if (ex.Headers is null)
            return null;

        foreach (var header in ex.Headers)
        {
            if (!string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
                continue;

            var values = header.Value;
            if (values is null)
                continue;

            foreach (var value in values)
            {
                if (TryParseRetryAfterValue(value, out var delay))
                    return delay;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a Retry-After header value as either seconds or an HTTP date.
    /// </summary>
    /// <param name="value">Retry-After header value.</param>
    /// <param name="delay">Parsed positive delay when successful.</param>
    /// <returns><see langword="true"/> when the value contains a positive retry delay.</returns>
    private static bool TryParseRetryAfterValue(string? value, out TimeSpan delay)
    {
        delay = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            delay = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var when))
        {
            var span = when - DateTimeOffset.UtcNow;
            if (span > TimeSpan.Zero)
            {
                delay = span;
                return true;
            }
        }

        return false;
    }
}
