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
/// Two-factor state detection, normalization, and persistence helpers for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Applies pending two-factor state from a current-user response and filters completed methods.
    /// </summary>
    /// <param name="response">VRChat current-user response that may include verification prompts.</param>
    private void ApplyTwoFactorState(ApiResponse<CurrentUser> response)
    {
        _twoFactorMethods = GetTwoFactorMethods(response);
        _requiresLoginPlaceVerification = false;
        _requiresEmailCode = HasTwoFactorMethod(_twoFactorMethods, "emailOtp");
        _requiresTwoFactor = _twoFactorMethods.Count > 0
            || ResponseIndicatesTwoFactor(response)
            || _requiresEmailCode;

        ApplyCompletedTwoFactorFilter();
    }

    // Older builds modelled VRChat's new-login-location challenge as a click-the-link "login place"
    // step. VRChat actually delivers it as an emailOtp code, so migrate any persisted login-place
    // state to an email-code prompt on restore (matches VRCX and the official SDK).
    /// <summary>
    /// Migrates legacy login-place state to the current emailOtp verification model.
    /// </summary>
    private void NormalizeLegacyLoginPlaceState()
    {
        var hadLoginPlaceMethod = HasTwoFactorMethod(_twoFactorMethods, "loginPlace");
        if (!_requiresLoginPlaceVerification && !hadLoginPlaceMethod)
            return;

        _requiresLoginPlaceVerification = false;
        SetMetadata(MetadataRequiresLoginPlaceVerification, null);

        if (hadLoginPlaceMethod)
            _twoFactorMethods.RemoveAll(method => string.Equals(method, "loginPlace", StringComparison.OrdinalIgnoreCase));

        _requiresTwoFactor = true;
        _requiresEmailCode = true;
        AddTwoFactorMethod(_twoFactorMethods, "emailOtp");
    }

    /// <summary>
    /// Converts ambiguous restored pending-verification state into an emailOtp prompt when no user is authenticated.
    /// </summary>
    private void NormalizeLegacyPendingVerificationState()
    {
        if (!_requiresTwoFactor
            || _requiresLoginPlaceVerification
            || _twoFactorMethods.Count > 0
            || _completedTwoFactorMethods.Count > 0
            || !_lastVerifiedUtc.HasValue
            || HasAuthenticatedUser())
        {
            return;
        }

        // The stored session was previously verified but no longer returns a user and VRChat did not
        // report which factor it wants. The new-location follow-up is an emailOtp code, so prompt for
        // that rather than a non-existent click-the-link step.
        MarkTwoFactorMethodCompleted("totp", "otp");
        _requiresLoginPlaceVerification = false;
        _requiresEmailCode = true;
        AddTwoFactorMethod(_twoFactorMethods, "emailOtp");
        _lastVerifiedUtc = null;
        SetMetadata(MetadataLastVerifiedUtc, null);
    }

    /// <summary>
    /// Removes completed two-factor methods from the pending list and persists the resulting verification metadata.
    /// </summary>
    private void ApplyCompletedTwoFactorFilter()
    {
        if (_twoFactorMethods.Count > 0 && _completedTwoFactorMethods.Count > 0)
        {
            var remaining = new List<string>();
            foreach (var method in _twoFactorMethods)
                if (!HasTwoFactorMethod(_completedTwoFactorMethods, method))
                    AddTwoFactorMethod(remaining, method);

            _twoFactorMethods = remaining;
        }

        _requiresEmailCode = _requiresLoginPlaceVerification
            || HasTwoFactorMethod(_twoFactorMethods, "emailOtp");

        if (_requiresTwoFactor
            && _twoFactorMethods.Count == 0
            && !_requiresLoginPlaceVerification
            && (HasCompletedTwoFactorMethod("totp") || HasCompletedTwoFactorMethod("otp"))
            && !HasAuthenticatedUser())
        {
            ApplyPendingCookieWithoutUserState();
            return;
        }

        if (!_requiresTwoFactor && (_requiresEmailCode || _requiresLoginPlaceVerification || _twoFactorMethods.Count > 0))
            _requiresTwoFactor = true;

        PersistVerificationMetadata();
    }

    /// <summary>
    /// Marks a session with cookies but no user as still pending a follow-up verification step.
    /// </summary>
    private void ApplyPendingCookieWithoutUserState()
    {
        _requiresTwoFactor = true;

        if (HasCompletedTwoFactorMethod("totp") || HasCompletedTwoFactorMethod("otp"))
        {
            // After the authenticator step VRChat can still require the new-login-location email code.
            // Surface it as an emailOtp code entry (the user types the code from the email) rather than a
            // click-the-link step, matching VRChat's real behaviour and how VRCX / the SDK handle it.
            _requiresLoginPlaceVerification = false;
            _requiresEmailCode = true;
            _twoFactorMethods = [];
            AddTwoFactorMethod(_twoFactorMethods, "emailOtp");
        }

        PersistVerificationMetadata();
    }

    /// <summary>
    /// Writes current pending-verification flags and method lists into persisted metadata.
    /// </summary>
    private void PersistVerificationMetadata()
    {
        SetMetadata(MetadataRequiresEmail2Fa, _requiresEmailCode ? "true" : null);
        SetMetadata(MetadataPendingTwoFactor, _requiresTwoFactor ? "true" : null);
        SetMetadata(MetadataTwoFactorMethods, _twoFactorMethods.Count > 0 ? string.Join(",", _twoFactorMethods) : null);
        SetMetadata(MetadataCompletedTwoFactorMethods, _completedTwoFactorMethods.Count > 0 ? string.Join(",", _completedTwoFactorMethods) : null);
        SetMetadata(MetadataRequiresLoginPlaceVerification, _requiresLoginPlaceVerification ? "true" : null);
    }

    /// <summary>
    /// Adds one or more method names to the completed two-factor list and persists the list.
    /// </summary>
    /// <param name="methods">Method names to mark as completed.</param>
    private void MarkTwoFactorMethodCompleted(params string[] methods)
    {
        foreach (var method in methods)
            AddTwoFactorMethod(_completedTwoFactorMethods, method);

        SetMetadata(MetadataCompletedTwoFactorMethods, _completedTwoFactorMethods.Count > 0 ? string.Join(",", _completedTwoFactorMethods) : null);
    }

    /// <summary>
    /// Checks whether a two-factor method has already been completed.
    /// </summary>
    /// <param name="method">Method name to check.</param>
    /// <returns><see langword="true"/> when the method appears in the completed list.</returns>
    private bool HasCompletedTwoFactorMethod(string method)
        => HasTwoFactorMethod(_completedTwoFactorMethods, method);

    /// <summary>
    /// Reads pending two-factor method names from typed SDK data and raw response JSON.
    /// </summary>
    /// <param name="response">VRChat current-user response.</param>
    /// <returns>Unique pending method names.</returns>
    private static List<string> GetTwoFactorMethods(ApiResponse<CurrentUser> response)
    {
        var methods = new List<string>();

        if (response.Data?.RequiresTwoFactorAuth is not null)
            foreach (var available in response.Data.RequiresTwoFactorAuth)
                AddTwoFactorMethod(methods, Convert.ToString(available, CultureInfo.InvariantCulture));
        
        AddTwoFactorMethodsFromRaw(methods, response.RawContent);
        return methods;
    }

    /// <summary>
    /// Parses persisted comma-separated two-factor method metadata.
    /// </summary>
    /// <param name="value">Comma-separated method names.</param>
    /// <returns>Unique method names in insertion order.</returns>
    private static List<string> ParseTwoFactorMethods(string? value)
    {
        var methods = new List<string>();
        if (string.IsNullOrWhiteSpace(value))
            return methods;

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AddTwoFactorMethod(methods, part);

        return methods;
    }

    /// <summary>
    /// Adds pending two-factor method names found in raw current-user JSON.
    /// </summary>
    /// <param name="methods">Method list to update.</param>
    /// <param name="rawContent">Raw response content from VRChat.</param>
    private static void AddTwoFactorMethodsFromRaw(List<string> methods, string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return;

        try
        {
            using var document = JsonDocument.Parse(rawContent);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "requiresTwoFactorAuth", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            AddTwoFactorMethod(methods, item.GetString());
                    }
                }
                else if (property.Value.ValueKind == JsonValueKind.String)
                {
                    AddTwoFactorMethod(methods, property.Value.GetString());
                }
            }
        }
        catch (JsonException)
        {
            if (ContainsInsensitive(rawContent, "emailOtp"))
                AddTwoFactorMethod(methods, "emailOtp");
            if (ContainsInsensitive(rawContent, "totp"))
                AddTwoFactorMethod(methods, "totp");
        }
    }

    /// <summary>
    /// Adds a two-factor method name to a list when it is nonblank and not already present.
    /// </summary>
    /// <param name="methods">Method list to update.</param>
    /// <param name="method">Method name to add.</param>
    private static void AddTwoFactorMethod(List<string> methods, string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
            return;

        foreach (var existing in methods)
            if (string.Equals(existing, method, StringComparison.OrdinalIgnoreCase))
                return;

        methods.Add(method);
    }

    /// <summary>
    /// Checks whether a method sequence contains a given method name ignoring case.
    /// </summary>
    /// <param name="methods">Method names to search.</param>
    /// <param name="method">Method name to find.</param>
    /// <returns><see langword="true"/> when the method is present.</returns>
    private static bool HasTwoFactorMethod(IEnumerable<string> methods, string method)
    {
        foreach (var available in methods)
            if (string.Equals(available, method, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>
    /// Determines whether a response indicates VRChat is waiting for an email one-time code.
    /// </summary>
    /// <param name="response">VRChat current-user response.</param>
    /// <returns><see langword="true"/> when emailOtp appears in typed or raw response data.</returns>
    private static bool ResponseIndicatesEmail2Fa(ApiResponse<CurrentUser> response)
        => CurrentUserHasTwoFactorMethod(response?.Data, "emailOtp")
           || ContainsInsensitive(response?.RawContent, "emailOtp");

    /// <summary>
    /// Determines whether a response indicates any two-factor challenge.
    /// </summary>
    /// <param name="response">VRChat current-user response.</param>
    /// <returns><see langword="true"/> when typed or raw response data contains two-factor indicators.</returns>
    private static bool ResponseIndicatesTwoFactor(ApiResponse<CurrentUser> response)
        => CurrentUserHasAnyTwoFactorMethod(response?.Data)
           || ContainsInsensitive(response?.RawContent, "requiresTwoFactorAuth")
           || ContainsInsensitive(response?.RawContent, "otp");

    /// <summary>
    /// Checks whether a current-user object has any required two-factor methods.
    /// </summary>
    /// <param name="user">Current-user object from the VRChat SDK.</param>
    /// <returns><see langword="true"/> when at least one two-factor method is present.</returns>
    private static bool CurrentUserHasAnyTwoFactorMethod(CurrentUser? user)
        => user?.RequiresTwoFactorAuth is { Count: > 0 };

    /// <summary>
    /// Checks whether a current-user object lists a specific two-factor method.
    /// </summary>
    /// <param name="user">Current-user object from the VRChat SDK.</param>
    /// <param name="method">Method name to search for.</param>
    /// <returns><see langword="true"/> when the method is present.</returns>
    private static bool CurrentUserHasTwoFactorMethod(CurrentUser? user, string method)
    {
        if (user?.RequiresTwoFactorAuth is null || string.IsNullOrWhiteSpace(method))
            return false;

        foreach (var available in user.RequiresTwoFactorAuth)
            if (string.Equals(Convert.ToString(available, CultureInfo.InvariantCulture), method, StringComparison.OrdinalIgnoreCase))
                return true;
        
        return false;
    }

    /// <summary>
    /// Performs a case-insensitive substring check that treats null or blank sources as missing.
    /// </summary>
    /// <param name="source">String to search.</param>
    /// <param name="value">Substring to find.</param>
    /// <returns><see langword="true"/> when the source contains the value.</returns>
    private static bool ContainsInsensitive(string? source, string value)
        => !string.IsNullOrWhiteSpace(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
}
