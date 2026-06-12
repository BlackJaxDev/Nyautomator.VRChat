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
/// Verification-step handlers for <see cref="VRChatAuthService"/> login flows.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Submits an authenticator two-factor code and finalizes or advances the login flow.
    /// </summary>
    /// <param name="code">Authenticator code entered by the user.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A result containing the updated authentication or pending-step state.</returns>
    public async Task<VRChatOperationResult> VerifyTotpAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return VRChatOperationResult.CreateFailure("Two-factor code is required.", BuildStatus());

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var request = new TwoFactorAuthCode(code.Trim());
            ApiResponse<Verify2FAResult> response;
            try
            {
                EmitInfo("TwoFactorVerificationStarted", "Submitting VRChat authenticator code.");
                response = await ExecuteWithRateLimitRetryAsync(
                    () => _authenticationApi.Verify2FAWithHttpInfoAsync(request, cancellationToken: cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                var friendly = VRChatErrorTranslator.Translate(ex);
                var detail = VRChatErrorTranslator.BuildDetail(ex);
                _lastError = $"VRChat 2FA verification failed: {friendly}";
                EmitError("TwoFactorVerificationFailed", _lastError, detail);
                return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
            }

            if (response.Data?.Verified != true)
            {
                _lastError = "VRChat 2FA verification failed.";
                EmitError("TwoFactorVerificationFailed", _lastError, "VRChat API returned an unverified response.");
                return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
            }

            UpdateCookies(response.Cookies);
            MarkTwoFactorMethodCompleted("totp", "otp");
            var status = await FinalizeVerificationAsync(cancellationToken).ConfigureAwait(false);
            _lastError = null;
            var message = status.IsConnected
                ? "Two-factor verification completed."
                : status.RequiresLoginPlaceVerification
                    ? "Authenticator verified. VRChat detected a login from a new location and emailed you a verification link. Open that email, click the link to approve this login, then press Retry."
                    : status.RequiresEmailCode
                        ? "Authenticator verified. Enter the VRChat email code to finish signing in."
                        : "Authenticator verified, but VRChat still needs another verification step.";

            EmitInfo("TwoFactorVerificationCompleted", message);
            if (!status.IsConnected)
                EmitWarning("TwoFactorVerificationPending", message, $"Active methods: {string.Join(", ", status.TwoFactorMethods)}");

            return VRChatOperationResult.CreateSuccess(status, message);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Submits a VRChat email one-time code or rechecks legacy login-place approval when no code is supplied.
    /// </summary>
    /// <param name="code">Email code entered by the user, or blank to recheck legacy login-place state.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A result containing the updated authentication or pending-step state.</returns>
    public async Task<VRChatOperationResult> VerifyEmailAsync(string code, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // VRChat delivers its new-login-location challenge as an email one-time code (emailOtp),
            // the same way VRCX and the official SDK handle it (POST /auth/twofactorauth/emailotp/verify).
            // Always submit the code the user entered. Only fall back to a passive session re-check when
            // there is genuinely no code to submit (e.g. a legacy link approved out-of-band in a browser).
            if (string.IsNullOrWhiteSpace(code))
            {
                if (_requiresLoginPlaceVerification)
                    return await RecheckLoginPlaceInternalAsync(cancellationToken).ConfigureAwait(false);

                return VRChatOperationResult.CreateFailure("Email verification code is required.", BuildStatus());
            }

            var trimmedCode = code.Trim();
            try
            {
                EmitInfo("EmailVerificationStarted", "Submitting VRChat email verification code.");
                var request = new TwoFactorEmailCode(trimmedCode);
                var response = await ExecuteWithRateLimitRetryAsync(
                    () => _authenticationApi.Verify2FAEmailCodeWithHttpInfoAsync(request, cancellationToken: cancellationToken),
                    cancellationToken).ConfigureAwait(false);

                if (response.Data?.Verified != true)
                {
                    _lastError = "VRChat email verification failed.";
                    EmitError("EmailVerificationFailed", _lastError, "VRChat API returned an unverified response.");
                    return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
                }

                UpdateCookies(response.Cookies);
                MarkTwoFactorMethodCompleted("emailOtp");
            }
            catch (ApiException ex)
            {
                var friendly = VRChatErrorTranslator.Translate(ex);
                var detail = VRChatErrorTranslator.BuildDetail(ex);
                _lastError = $"VRChat email verification failed: {friendly}";
                EmitError("EmailVerificationFailed", _lastError, detail);
                return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
            }

            var status = await FinalizeVerificationAsync(cancellationToken).ConfigureAwait(false);
            _lastError = null;
            var message = status.IsConnected
                ? "Email verification completed."
                : "Verification accepted, but VRChat still needs another verification step.";
            EmitInfo("EmailVerificationCompleted", message);
            if (!status.IsConnected)
                EmitWarning("VerificationPending", message, $"Active methods: {string.Join(", ", status.TwoFactorMethods)}");

            return VRChatOperationResult.CreateSuccess(status, message);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Rechecks whether VRChat login-place approval has completed and finalizes the session if possible.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the recheck.</param>
    /// <returns>A result containing either connected or still-pending state.</returns>
    public async Task<VRChatOperationResult> RecheckLoginPlaceAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RecheckLoginPlaceInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Submits a login-place verification token or link to VRChat, then rechecks the session state.
    /// </summary>
    /// <param name="tokenOrUrl">Raw token, query string, or verification URL from the VRChat email.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A result containing the updated authentication state.</returns>
    public async Task<VRChatOperationResult> VerifyLoginPlaceTokenAsync(string tokenOrUrl, CancellationToken cancellationToken)
    {
        var parsed = ParseLoginPlaceVerification(tokenOrUrl);
        if (string.IsNullOrWhiteSpace(parsed.Token))
            return VRChatOperationResult.CreateFailure("VRChat login verification link or token is required.", BuildStatus());

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                EmitInfo("LoginPlaceTokenVerificationStarted", "Submitting VRChat login location verification token.");
                await _rateLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);
                await _authenticationApi
                    .VerifyLoginPlaceWithHttpInfoAsync(parsed.Token, parsed.UserId, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ApiException ex) when (ex.ErrorCode is >= 300 and < 400)
            {
                // VRChat documents this endpoint as returning a redirect after accepting the token.
            }
            catch (ApiException ex)
            {
                var friendly = VRChatErrorTranslator.Translate(ex);
                var detail = VRChatErrorTranslator.BuildDetail(ex);
                _lastError = $"VRChat login link verification failed: {friendly}";
                EmitError("LoginPlaceTokenVerificationFailed", _lastError, detail);
                return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
            }

            var result = await RecheckLoginPlaceInternalAsync(cancellationToken).ConfigureAwait(false);
            if (result.Status.IsConnected)
                EmitInfo("LoginPlaceTokenVerificationCompleted", "Login location verification token accepted.");
            return result;
        }
        finally
        {
            _sync.Release();
        }
    }

    // Assumes the caller already holds _sync. Re-checks whether the user has approved the new login
    // location (by clicking the emailed verification link) and finalizes the session if so.
    /// <summary>
    /// Rechecks login-place approval while the caller already holds the auth-state semaphore.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the recheck.</param>
    /// <returns>A result describing whether verification has completed or remains pending.</returns>
    private async Task<VRChatOperationResult> RecheckLoginPlaceInternalAsync(CancellationToken cancellationToken)
    {
        VRChatStatus status;
        try
        {
            EmitInfo("LoginPlaceRecheckStarted", "Re-checking VRChat login location approval.");
            status = await FinalizeVerificationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            var friendly = VRChatErrorTranslator.Translate(ex);
            var detail = VRChatErrorTranslator.BuildDetail(ex);
            _lastError = $"VRChat login verification check failed: {friendly}";
            EmitError("LoginPlaceVerificationFailed", _lastError, detail);
            return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
        }

        _lastError = null;

        if (status.IsConnected)
        {
            EmitInfo("LoginPlaceVerificationCompleted", "Login location approved.");
            return VRChatOperationResult.CreateSuccess(status, "Login location approved. VRChat sign-in complete.");
        }

        const string pending = "VRChat is still waiting for you to approve this login. Open the verification email VRChat sent and click the link, then press Retry. If no email arrived, check spam or request a new login email.";
        EmitWarning("LoginPlaceVerificationPending", pending, $"Active methods: {string.Join(", ", status.TwoFactorMethods)}");
        return VRChatOperationResult.CreateSuccess(status, pending);
    }

    /// <summary>
    /// Parses a login-place verification token from a raw token, query string, or full URL.
    /// </summary>
    /// <param name="tokenOrUrl">Raw token, query string, or verification URL.</param>
    /// <returns>The extracted token and optional user id.</returns>
    private static (string? Token, string? UserId) ParseLoginPlaceVerification(string? tokenOrUrl)
    {
        var trimmed = tokenOrUrl?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (null, null);

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            var token = GetQueryParameter(uri.Query, "token");
            var userId = GetQueryParameter(uri.Query, "userId");
            return (string.IsNullOrWhiteSpace(token) ? null : token, userId);
        }

        if (trimmed.Contains('=') || trimmed.Contains('&'))
        {
            var token = GetQueryParameter(trimmed, "token");
            var userId = GetQueryParameter(trimmed, "userId");
            if (!string.IsNullOrWhiteSpace(token))
                return (token, userId);
        }

        return (trimmed, null);
    }
}
