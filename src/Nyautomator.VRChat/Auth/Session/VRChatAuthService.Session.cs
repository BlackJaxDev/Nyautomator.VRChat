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
/// Session import, logout, and status operations for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Imports VRChat auth cookies and validates them by refreshing the current user.
    /// </summary>
    /// <param name="authCookieOrHeader">Raw auth cookie value or cookie header.</param>
    /// <param name="twoFactorCookieOrHeader">Optional raw two-factor cookie value or cookie header.</param>
    /// <param name="cancellationToken">Token that cancels import validation.</param>
    /// <returns>A result describing whether the cookies produced an authenticated or pending session.</returns>
    public async Task<VRChatOperationResult> ImportSessionCookiesAsync(
        string authCookieOrHeader,
        string? twoFactorCookieOrHeader,
        CancellationToken cancellationToken)
    {
        var authCookie = ExtractCookieValue(authCookieOrHeader, "auth");
        var twoFactorCookie = ExtractCookieValue(twoFactorCookieOrHeader, "twoFactorAuth")
            ?? ExtractCookieValue(authCookieOrHeader, "twoFactorAuth");

        if (string.IsNullOrWhiteSpace(authCookie))
            return VRChatOperationResult.CreateFailure("VRChat auth cookie is required.", BuildStatus());

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ResetState(clearMetadata: true);
            SetCookie("auth", authCookie);
            SetMetadata(MetadataAuthCookie, authCookie);

            if (!string.IsNullOrWhiteSpace(twoFactorCookie))
            {
                SetCookie("twoFactorAuth", twoFactorCookie);
                SetMetadata(MetadataTwoFactorCookie, twoFactorCookie);
            }

            try
            {
                EmitInfo("SessionCookieImportStarted", "Importing VRChat browser session cookies.");
                await RefreshCurrentUserInternalAsync(cancellationToken, force: true).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                HandleApiException(ex, clearSessionOnUnauthorized: true);
                return VRChatOperationResult.CreateFailure(
                    _lastError ?? "VRChat session cookie import failed.",
                    BuildStatus());
            }

            PersistToken();
            var status = BuildStatus();
            if (status.IsConnected)
            {
                EmitInfo("SessionCookieImportCompleted", $"VRChat session imported for {DescribeIdentity(status)}.");
                return VRChatOperationResult.CreateSuccess(status, "VRChat browser session imported.");
            }

            var message = status.RequiresTwoFactor
                ? "VRChat accepted the session cookie, but another verification step is still required."
                : "VRChat session cookie import did not return an authenticated user.";
            _lastError = message;
            EmitWarning("SessionCookieImportIncomplete", message, $"Active methods: {string.Join(", ", status.TwoFactorMethods)}");
            return VRChatOperationResult.CreateFailure(message, BuildStatus());
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Reports that VRChat has no standalone email-code resend endpoint usable without a fresh login attempt.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the status update.</param>
    /// <returns>A failed operation result explaining how to request a new email code.</returns>
    public async Task<VRChatOperationResult> ResendEmailAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            const string message = "VRChat does not expose a standalone resend endpoint for login or 2FA email codes. Enter your password and request a new login email so Nyautomator can start a fresh login attempt.";
            _lastError = message;
            EmitWarning("EmailResendUnavailable", message);
            return VRChatOperationResult.CreateFailure(message, BuildStatus());
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Clears in-memory and persisted VRChat session state.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels logout state mutation.</param>
    /// <returns>A successful operation result containing the cleared status.</returns>
    public async Task<VRChatOperationResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ResetState(clearMetadata: true);
            _sessionStore.Clear();
            _lastError = null;
            var status = BuildStatus();
            EmitInfo("Logout", "VRChat session cleared.");
            return VRChatOperationResult.CreateSuccess(status, "VRChat session cleared.");
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Gets the current authentication status, optionally refreshing the current user from VRChat first.
    /// </summary>
    /// <param name="refresh">Whether to refresh current-user state before building the status.</param>
    /// <param name="cancellationToken">Token that cancels the optional refresh.</param>
    /// <returns>The latest authentication status snapshot.</returns>
    public async Task<VRChatStatus> GetStatusAsync(bool refresh, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (refresh)
            {
                try
                {
                    await RefreshCurrentUserInternalAsync(cancellationToken).ConfigureAwait(false);
                    _lastError = HasAuthenticatedUser() || _requiresTwoFactor
                        ? null
                        : "Stored VRChat session did not return an authenticated user.";
                }
                catch (ApiException ex)
                {
                    HandleApiException(ex, clearSessionOnUnauthorized: true);
                }
            }

            return BuildStatus();
        }
        finally
        {
            _sync.Release();
        }
    }
}
