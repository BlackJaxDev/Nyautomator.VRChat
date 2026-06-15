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
    /// Submits a VRChat email one-time code.
    /// </summary>
    /// <param name="code">Email code entered by the user.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A result containing the updated authentication or pending-step state.</returns>
    public async Task<VRChatOperationResult> VerifyEmailAsync(string code, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(code))
                return VRChatOperationResult.CreateFailure("Email verification code is required.", BuildStatus());

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
}
