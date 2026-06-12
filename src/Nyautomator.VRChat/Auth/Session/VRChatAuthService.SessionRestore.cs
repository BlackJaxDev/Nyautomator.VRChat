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
/// Stored-session restoration logic for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Restores persisted VRChat session metadata and optionally refreshes it against the current-user API.
    /// </summary>
    /// <param name="forceRefresh">Whether to call VRChat after loading persisted metadata.</param>
    /// <param name="cancellationToken">Token that cancels refresh work.</param>
    /// <returns>The restored authentication status snapshot.</returns>
    public async Task<VRChatStatus> TryRestoreSessionAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var token = _sessionStore.Get();
            if (token is null)
            {
                ResetState(clearMetadata: true);
                return BuildStatus();
            }

            _metadata = token.Metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(token.Metadata, StringComparer.OrdinalIgnoreCase);

            _cachedLogin = token.AccountLogin ?? GetMetadata(MetadataAccountLogin);
            if (!string.IsNullOrWhiteSpace(_cachedLogin))
                SetMetadata(MetadataAccountLogin, _cachedLogin);

            _cachedUserId = token.AccountId ?? GetMetadata(MetadataUserId);
            _cachedDisplayName = token.AccountDisplayName ?? GetMetadata(MetadataDisplayName);
            _cachedEmailHint = GetMetadata(MetadataEmailHint);
            _cachedPendingEmailHint = GetMetadata(MetadataPendingEmailHint);
            _updatedAtUtc = token.UpdatedAtUtc ?? DateTime.UtcNow;
            _lastVerifiedUtc = ParseUtc(GetMetadata(MetadataLastVerifiedUtc));
            _requiresTwoFactor = GetMetadataBool(MetadataPendingTwoFactor);
            _requiresEmailCode = GetMetadataBool(MetadataRequiresEmail2Fa);
            _requiresLoginPlaceVerification = GetMetadataBool(MetadataRequiresLoginPlaceVerification);
            _twoFactorMethods = ParseTwoFactorMethods(GetMetadata(MetadataTwoFactorMethods));
            _completedTwoFactorMethods = ParseTwoFactorMethods(GetMetadata(MetadataCompletedTwoFactorMethods));
            NormalizeLegacyLoginPlaceState();
            NormalizeLegacyPendingVerificationState();
            ApplyCompletedTwoFactorFilter();
            _lastError = null;

            ApplyCookiesFromMetadata();

            if (forceRefresh)
            {
                try
                {
                    await RefreshCurrentUserInternalAsync(cancellationToken, force: true).ConfigureAwait(false);
                    _lastError = HasAuthenticatedUser() || _requiresTwoFactor
                        ? null
                        : "Stored VRChat session did not return an authenticated user.";
                }
                catch (ApiException ex)
                {
                    HandleApiException(ex, clearSessionOnUnauthorized: true);
                }
            }

            if (CookiesExpired())
            {
                _sessionStore.Clear();
                ResetState(clearMetadata: true);
                _lastError = "Stored VRChat session has expired.";
                EmitWarning("SessionExpired", "Stored VRChat session expired and was cleared.");
            }

            var status = BuildStatus();

            if (forceRefresh && status.IsConnected && !_requiresTwoFactor)
            {
                EmitInfo("SessionRestored", $"VRChat session restored for {DescribeIdentity(status)}.");
            }

            if (forceRefresh && status.RequiresTwoFactor)
            {
                var factor = status.RequiresLoginPlaceVerification
                    ? "new login location approval (check email link)"
                    : status.RequiresEmailCode ? "email verification" : "two-factor code";
                EmitWarning("SessionAwaitingVerification", $"Stored VRChat session is awaiting {factor} for {DescribeIdentity(status)}.");
            }

            return status;
        }
        finally
        {
            _sync.Release();
        }
    }
}
