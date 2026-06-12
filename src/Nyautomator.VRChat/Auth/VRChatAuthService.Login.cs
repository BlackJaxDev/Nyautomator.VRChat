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
/// Username and password login flow for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Attempts a VRChat username/password login and records any required verification state.
    /// </summary>
    /// <param name="username">VRChat username or email to submit.</param>
    /// <param name="password">VRChat password to submit.</param>
    /// <param name="cancellationToken">Token that cancels the login request.</param>
    /// <returns>A result containing success, pending verification, or failure state.</returns>
    public async Task<VRChatOperationResult> LoginAsync(string username, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return VRChatOperationResult.CreateFailure("Username and password are required.", BuildStatus());

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var trimmedUser = username.Trim();
            EmitInfo("LoginRequested", $"Attempting VRChat login for '{trimmedUser}'.");
            ResetState(clearMetadata: true);

            _configuration.Username = username;
            _configuration.Password = password;

            ApiResponse<CurrentUser> response;
            try
            {
                response = await ExecuteWithRateLimitRetryAsync(
                    () => _authenticationApi.GetCurrentUserWithHttpInfoAsync(cancellationToken: cancellationToken),
                    cancellationToken,
                    retryRateLimit: false).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                var friendly = VRChatErrorTranslator.Translate(ex);
                var detail = VRChatErrorTranslator.BuildDetail(ex);
                _lastError = ex.ErrorCode == 429
                    ? "VRChat is temporarily rate limiting sign-in attempts (too many tries in a short time). Wait a few minutes before trying again."
                    : $"VRChat login failed: {friendly}";
                EmitError("LoginFailed", _lastError, detail);
                return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
            }
            finally
            {
                _configuration.Username = string.Empty;
                _configuration.Password = string.Empty;
            }

            UpdateCookies(response.Cookies);
            ProcessCurrentUser(response);

            _cachedLogin = username.Trim();
            SetMetadata(MetadataAccountLogin, _cachedLogin);

            ApplyTwoFactorState(response);

            if (!_requiresTwoFactor)
            {
                if (_currentUser is null)
                {
                    try
                    {
                        await RefreshCurrentUserInternalAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (ApiException refreshEx)
                    {
                        HandleApiException(refreshEx, clearSessionOnUnauthorized: true);
                        return VRChatOperationResult.CreateFailure(
                            _lastError ?? "VRChat login failed while verifying the session.",
                            BuildStatus());
                    }
                }

                if (!HasAuthenticatedUser() && HasPendingAuthCookie())
                {
                    ApplyPendingCookieWithoutUserState();
                }

                if (!HasAuthenticatedUser() && !_requiresTwoFactor)
                {
                    const string incompleteMessage = "VRChat login did not return an authenticated user or a supported verification challenge.";
                    var safeDiagnostic = BuildLoginSafeDiagnostic(response);
                    ResetState(clearMetadata: true);
                    _sessionStore.Clear();
                    _lastError = $"{incompleteMessage} ({safeDiagnostic})";
                    EmitError("LoginIncomplete", incompleteMessage, BuildLoginDiagnostic(response));
                    return VRChatOperationResult.CreateFailure(_lastError, BuildStatus());
                }

                if (!_requiresTwoFactor && HasAuthenticatedUser())
                {
                    _lastVerifiedUtc = DateTime.UtcNow;
                    SetMetadata(MetadataLastVerifiedUtc, _lastVerifiedUtc?.ToString("o", CultureInfo.InvariantCulture));
                }
            }

            PersistToken();
            _lastError = null;
            var status = BuildStatus();

            if (_requiresTwoFactor)
            {
                var factor = _requiresEmailCode ? "email code" : "two-factor code";
                EmitWarning("TwoFactorRequired", $"VRChat login for '{trimmedUser}' requires a {factor}.", BuildLoginSafeDiagnostic(response));
            }
            else
            {
                EmitInfo("LoginSucceeded", $"VRChat login completed for {DescribeIdentity(status)}.");
            }

            return VRChatOperationResult.CreateSuccess(status);
        }
        finally
        {
            _sync.Release();
        }
    }
}
