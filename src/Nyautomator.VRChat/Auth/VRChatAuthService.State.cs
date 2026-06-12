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
/// Internal state transitions and status construction for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Clears pending verification flags, refreshes current-user data, and persists the verified session.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the current-user refresh.</param>
    /// <returns>The updated authentication status.</returns>
    private async Task<VRChatStatus> FinalizeVerificationAsync(CancellationToken cancellationToken)
    {
        _requiresTwoFactor = false;
        _requiresEmailCode = false;
        _requiresLoginPlaceVerification = false;
        _twoFactorMethods = [];
        SetMetadata(MetadataPendingTwoFactor, null);
        SetMetadata(MetadataRequiresEmail2Fa, null);
        SetMetadata(MetadataRequiresLoginPlaceVerification, null);
        SetMetadata(MetadataTwoFactorMethods, null);

        await RefreshCurrentUserInternalAsync(cancellationToken, force: true).ConfigureAwait(false);

        if (!_requiresTwoFactor && HasAuthenticatedUser())
        {
            _completedTwoFactorMethods = [];
            _requiresLoginPlaceVerification = false;
            SetMetadata(MetadataCompletedTwoFactorMethods, null);
            SetMetadata(MetadataRequiresLoginPlaceVerification, null);
            _lastVerifiedUtc = DateTime.UtcNow;
            SetMetadata(MetadataLastVerifiedUtc, _lastVerifiedUtc?.ToString("o", CultureInfo.InvariantCulture));
        }

        PersistToken();
        return BuildStatus();
    }

    /// <summary>
    /// Refreshes the current-user endpoint unless cached data is still fresh and no force refresh is requested.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the VRChat API request.</param>
    /// <param name="force">Whether to bypass the current-user cache window.</param>
    /// <returns>A task that completes after current-user state has been updated or skipped.</returns>
    private async Task RefreshCurrentUserInternalAsync(CancellationToken cancellationToken, bool force = false)
    {
        if (!force && _currentUser is not null && _lastCurrentUserFetchUtc.HasValue)
        {
            var nextRefresh = _lastCurrentUserFetchUtc.Value.Add(CurrentUserCacheDuration);
            if (DateTime.UtcNow < nextRefresh)
                return;
        }

        var response = await ExecuteWithRateLimitRetryAsync(
            () => _authenticationApi.GetCurrentUserWithHttpInfoAsync(cancellationToken: cancellationToken),
            cancellationToken).ConfigureAwait(false);

        var wasPendingVerification = _requiresTwoFactor
            || _requiresEmailCode
            || _requiresLoginPlaceVerification
            || _completedTwoFactorMethods.Count > 0;

        UpdateCookies(response.Cookies);
        ApplyTwoFactorState(response);
        if (!_requiresTwoFactor && HasPendingAuthCookie() && !HasAuthenticatedUser())
        {
            ApplyPendingCookieWithoutUserState();
        }
        if (_requiresTwoFactor)
            return;

        ProcessCurrentUser(response);
        if (wasPendingVerification && HasAuthenticatedUser())
            MarkAuthenticatedSessionVerified();
    }

    /// <summary>
    /// Captures cookies, typed current-user fields, raw JSON fallbacks, and current-user cache time from a response.
    /// </summary>
    /// <param name="response">VRChat current-user response.</param>
    private void ProcessCurrentUser(ApiResponse<CurrentUser> response)
    {
        UpdateCookies(response.Cookies);
        _currentUser = response.Data;
        if (response.Data is not null)
        {
            if (!string.IsNullOrWhiteSpace(response.Data.Id))
            {
                _cachedUserId = response.Data.Id;
                SetMetadata(MetadataUserId, response.Data.Id);
            }

            if (!string.IsNullOrWhiteSpace(response.Data.DisplayName))
            {
                _cachedDisplayName = response.Data.DisplayName;
                SetMetadata(MetadataDisplayName, response.Data.DisplayName);
            }

            CaptureEmailHints(response.Data.ObfuscatedEmail, response.Data.ObfuscatedPendingEmail);
        }

        ProcessCurrentUserRaw(response.RawContent);

        if (HasAuthenticatedUser())
            _lastCurrentUserFetchUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Extracts user identity and email hints from raw current-user JSON when SDK model fields are incomplete.
    /// </summary>
    /// <param name="rawContent">Raw current-user response content.</param>
    private void ProcessCurrentUserRaw(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return;

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            var id = GetStringProperty(document.RootElement, "id");
            if (!string.IsNullOrWhiteSpace(id))
            {
                _cachedUserId = id;
                SetMetadata(MetadataUserId, id);
            }

            var displayName = GetStringProperty(document.RootElement, "displayName");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                _cachedDisplayName = displayName;
                SetMetadata(MetadataDisplayName, displayName);
            }

            CaptureEmailHints(
                GetStringProperty(document.RootElement, "obfuscatedEmail"),
                GetStringProperty(document.RootElement, "obfuscatedPendingEmail"));
        }
        catch (JsonException)
        {
            // Some VRChat auth responses are verification prompts rather than user JSON.
        }
    }

    /// <summary>
    /// Caches and persists VRChat's obfuscated account and pending-verification email hints.
    /// </summary>
    /// <param name="obfuscatedEmail">Masked account email hint.</param>
    /// <param name="obfuscatedPendingEmail">Masked pending challenge email hint.</param>
    private void CaptureEmailHints(string? obfuscatedEmail, string? obfuscatedPendingEmail)
    {
        if (!string.IsNullOrWhiteSpace(obfuscatedEmail))
        {
            _cachedEmailHint = obfuscatedEmail.Trim();
            SetMetadata(MetadataEmailHint, _cachedEmailHint);
        }

        if (!string.IsNullOrWhiteSpace(obfuscatedPendingEmail))
        {
            _cachedPendingEmailHint = obfuscatedPendingEmail.Trim();
            SetMetadata(MetadataPendingEmailHint, _cachedPendingEmailHint);
        }
    }

    /// <summary>
    /// Writes cached account identity and auth metadata to the integration token store.
    /// </summary>
    private void PersistToken()
    {
        var token = new VRChatSessionToken
        {
            AccountId = _cachedUserId,
            AccountDisplayName = _cachedDisplayName,
            AccountLogin = _cachedLogin,
            Metadata = new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase)
        };

        _sessionStore.Set(token);
        _updatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the current session as fully authenticated and clears pending verification metadata.
    /// </summary>
    private void MarkAuthenticatedSessionVerified()
    {
        _requiresTwoFactor = false;
        _requiresEmailCode = false;
        _requiresLoginPlaceVerification = false;
        _twoFactorMethods = [];
        _completedTwoFactorMethods = [];
        SetMetadata(MetadataPendingTwoFactor, null);
        SetMetadata(MetadataRequiresEmail2Fa, null);
        SetMetadata(MetadataRequiresLoginPlaceVerification, null);
        SetMetadata(MetadataTwoFactorMethods, null);
        SetMetadata(MetadataCompletedTwoFactorMethods, null);
        _lastVerifiedUtc = DateTime.UtcNow;
        SetMetadata(MetadataLastVerifiedUtc, _lastVerifiedUtc?.ToString("o", CultureInfo.InvariantCulture));
        PersistToken();
    }

    /// <summary>
    /// Builds a display label for logs from the freshest available in-memory account identity.
    /// </summary>
    /// <returns>A display name, login, user id, or generic account label.</returns>
    private string DescribeIdentity()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser?.DisplayName))
            return _currentUser!.DisplayName!;
        if (!string.IsNullOrWhiteSpace(_cachedDisplayName))
            return _cachedDisplayName!;
        if (!string.IsNullOrWhiteSpace(_cachedLogin))
            return _cachedLogin!;
        if (!string.IsNullOrWhiteSpace(_cachedUserId))
            return _cachedUserId!;
        return "VRChat account";
    }

    /// <summary>
    /// Builds a display label for logs from a status snapshot.
    /// </summary>
    /// <param name="status">Status snapshot containing account identity fields.</param>
    /// <returns>A display name, login, user id, or generic account label.</returns>
    private static string DescribeIdentity(VRChatStatus status)
    {
        if (!string.IsNullOrWhiteSpace(status.DisplayName))
            return status.DisplayName!;
        if (!string.IsNullOrWhiteSpace(status.AccountLogin))
            return status.AccountLogin!;
        if (!string.IsNullOrWhiteSpace(status.UserId))
            return status.UserId!;
        return "VRChat account";
    }

    /// <summary>
    /// Determines whether persisted cookie metadata is older than the configured cookie lifetime.
    /// </summary>
    /// <returns><see langword="true"/> when stored cookies should be treated as expired.</returns>
    private bool CookiesExpired()
    {
        if (!_updatedAtUtc.HasValue)
            return false;

        if (_options.CookieTtlDays <= 0)
            return false;

        return _updatedAtUtc.Value < DateTime.UtcNow.AddDays(-_options.CookieTtlDays);
    }

    /// <summary>
    /// Checks whether a current or cached authenticated user id is available.
    /// </summary>
    /// <returns><see langword="true"/> when an authenticated user id is known.</returns>
    private bool HasAuthenticatedUser()
        => !string.IsNullOrWhiteSpace(_currentUser?.Id) || !string.IsNullOrWhiteSpace(_cachedUserId);

    /// <summary>
    /// Clears in-memory authentication, verification, cookie, and cooldown state.
    /// </summary>
    /// <param name="clearMetadata">Whether persisted metadata should be cleared from memory too.</param>
    private void ResetState(bool clearMetadata)
    {
        _currentUser = null;
        _cachedUserId = null;
        _cachedDisplayName = null;
        _cachedEmailHint = null;
        _cachedPendingEmailHint = null;
        _requiresTwoFactor = false;
        _requiresEmailCode = false;
        _requiresLoginPlaceVerification = false;
        _twoFactorMethods = [];
        _completedTwoFactorMethods = [];
        _lastVerifiedUtc = null;
        _updatedAtUtc = null;
        _lastCurrentUserFetchUtc = null;
        _lastError = null;
        _failedRequests.Clear();

        SetCookie("auth", null);
        SetCookie("twoFactorAuth", null);

        if (clearMetadata)
            _metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the public status DTO from current in-memory and persisted auth state.
    /// </summary>
    /// <returns>The current VRChat authentication status.</returns>
    private VRChatStatus BuildStatus()
    {
        var displayName = _currentUser?.DisplayName ?? _cachedDisplayName;
        var userId = _currentUser?.Id ?? _cachedUserId;

        return new VRChatStatus
        {
            HasStoredSession = _metadata.Count > 0,
            IsConnected = !_requiresTwoFactor && !string.IsNullOrWhiteSpace(userId),
            RequiresTwoFactor = _requiresTwoFactor,
            RequiresEmailCode = _requiresEmailCode,
            RequiresLoginPlaceVerification = _requiresLoginPlaceVerification,
            TwoFactorMethods = [.. _twoFactorMethods],
            CompletedTwoFactorMethods = [.. _completedTwoFactorMethods],
            DisplayName = displayName,
            UserId = userId,
            AccountLogin = _cachedLogin,
            EmailHint = _cachedEmailHint,
            PendingEmailHint = _cachedPendingEmailHint,
            LastVerifiedUtc = _lastVerifiedUtc,
            UpdatedAtUtc = _updatedAtUtc,
            LastError = _lastError,
            AutoReconnect = _options.AutoReconnect
        };
    }

    /// <summary>
    /// Reads a metadata value by key.
    /// </summary>
    /// <param name="key">Metadata key to read.</param>
    /// <returns>The stored metadata value, or <see langword="null"/> when absent.</returns>
    private string? GetMetadata(string key)
        => _metadata.TryGetValue(key, out var value) ? value : null;

    /// <summary>
    /// Sets or removes a metadata value.
    /// </summary>
    /// <param name="key">Metadata key to update.</param>
    /// <param name="value">Value to store, or blank to remove the key.</param>
    private void SetMetadata(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            _metadata.Remove(key);
        else
            _metadata[key] = value;
    }

    /// <summary>
    /// Parses a persisted UTC timestamp using invariant culture.
    /// </summary>
    /// <param name="value">Timestamp text to parse.</param>
    /// <returns>The parsed UTC time, or <see langword="null"/> when parsing fails.</returns>
    private static DateTime? ParseUtc(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Reads a case-insensitive string property from a JSON object.
    /// </summary>
    /// <param name="element">JSON object to inspect.</param>
    /// <param name="propertyName">Property name to read.</param>
    /// <returns>The property string value, or <see langword="null"/> when absent or not a string.</returns>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a persisted boolean metadata value.
    /// </summary>
    /// <param name="value">Metadata value to parse.</param>
    /// <returns><see langword="true"/> only when the value parses as true.</returns>
    private static bool GetMetadataBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;
}
