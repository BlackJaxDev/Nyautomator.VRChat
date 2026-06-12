using Nyautomator.Module.Abstractions;
using ModuleAuthRequest = Nyautomator.Module.Abstractions.AuthenticatedIntegrationRequest;
using ModuleAuthResponse = Nyautomator.Module.Abstractions.AuthenticatedIntegrationResponse;

namespace Nyautomator.Modules.VRChat;

public sealed partial class VRChatModuleBridge
{
    /// <summary>
    /// Sends an authenticated VRChat cloud API request through the configured VRChat auth service.
    /// </summary>
    /// <param name="request">Authenticated integration request supplied by automation or module consumers.</param>
    /// <param name="cancellationToken">Token that cancels the authenticated request.</param>
    /// <returns>The VRChat response mapped to the module authenticated integration contract.</returns>
    public async Task<ModuleAuthResponse> SendAsync(ModuleAuthRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
                return new ModuleAuthResponse(false, 0, null, "VRChat configuration is not available.");

            var result = await _vrchat.Auth.SendAuthenticatedRequestAsync(
                request.Method,
                request.Path,
                request.Query,
                request.Body,
                request.ContentType,
                request.TimeoutMs,
                cancellationToken).ConfigureAwait(false);

            return new ModuleAuthResponse(
                result.Success,
                result.StatusCode,
                result.Body,
                result.Error,
                "application/json",
                ParseHeaders(result.Headers));
        }
        catch (OperationCanceledException)
        {
            return new ModuleAuthResponse(false, 0, null, "VRChat authenticated request cancelled or timed out.");
        }
        catch (Exception ex)
        {
            return new ModuleAuthResponse(false, 0, null, ex.Message);
        }
    }

    /// <summary>
    /// Returns the current VRChat authentication status, optionally forcing a refresh.
    /// </summary>
    /// <param name="request">Status request containing the optional refresh query value.</param>
    /// <param name="cancellationToken">Token that cancels the status request.</param>
    /// <returns>A JSON response containing the mapped VRChat status.</returns>
    private async Task<ModuleApiResponse> GetStatusAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var refresh = request.Query.TryGetValue("refresh", out var value)
            && bool.TryParse(value, out var parsed)
            && parsed;
        var status = await _vrchat.Auth.GetStatusAsync(refresh, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(new { success = true, status = ToDto(status) });
    }

    /// <summary>
    /// Reads username and password credentials and starts the VRChat login flow.
    /// </summary>
    /// <param name="request">Module API request whose body contains login credentials.</param>
    /// <param name="cancellationToken">Token that cancels the login request.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> LoginAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VRChatLoginRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
            return Error("Username and password are required.");

        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.LoginAsync(body.Username, body.Password, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Reads a TOTP code and submits it to the pending VRChat two-factor login flow.
    /// </summary>
    /// <param name="request">Module API request whose body contains a two-factor code.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> VerifyTotpAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VRChatCodeRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.Code))
            return Error("Two-factor code is required.");

        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.VerifyTotpAsync(body.Code, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Reads an email verification code and submits it to the pending VRChat login flow.
    /// </summary>
    /// <param name="request">Module API request whose body contains an email verification code.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> VerifyEmailAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VRChatCodeRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.Code))
            return Error("Email verification code is required.");

        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.VerifyEmailAsync(body.Code, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Rechecks whether VRChat login-place verification has completed externally.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> VerifyLoginPlaceAsync(CancellationToken cancellationToken)
    {
        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.RecheckLoginPlaceAsync(CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Reads a login-place verification token or link and submits it to the VRChat auth service.
    /// </summary>
    /// <param name="request">Module API request whose body contains a verification token or link.</param>
    /// <param name="cancellationToken">Token that cancels verification.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> VerifyLoginPlaceTokenAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VRChatCodeRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.Code))
            return Error("VRChat login verification link or token is required.");

        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.VerifyLoginPlaceTokenAsync(body.Code, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Imports VRChat session cookies into the auth service for reuse by authenticated requests.
    /// </summary>
    /// <param name="request">Module API request whose body contains auth and optional two-factor cookies.</param>
    /// <param name="cancellationToken">Token that cancels cookie import.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> ImportSessionAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<VRChatSessionCookieRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.AuthCookie))
            return Error("VRChat auth cookie is required.");

        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.ImportSessionCookiesAsync(body.AuthCookie, body.TwoFactorCookie, CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Resends the VRChat email challenge, optionally by retrying login when credentials are supplied.
    /// </summary>
    /// <param name="request">Module API request with optional login credentials.</param>
    /// <param name="cancellationToken">Token that cancels the resend request.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> ResendEmailAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var body = await ReadJsonAsync<VRChatLoginRequest>(request, cancellationToken).ConfigureAwait(false);
        var result = body is not null
            && !string.IsNullOrWhiteSpace(body.Username)
            && !string.IsNullOrWhiteSpace(body.Password)
                ? await _vrchat.Auth.LoginAsync(body.Username, body.Password, CreateRequestToken(cancellationToken)).ConfigureAwait(false)
                : await _vrchat.Auth.ResendEmailAsync(CreateRequestToken(cancellationToken)).ConfigureAwait(false);

        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Logs out of VRChat and clears the stored authentication session.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels logout.</param>
    /// <returns>A JSON response containing the VRChat operation result.</returns>
    private async Task<ModuleApiResponse> LogoutAsync(CancellationToken cancellationToken)
    {
        if (!await TryConfigureVRChatAsync(cancellationToken).ConfigureAwait(false))
            return Error("VRChat configuration is not available.", 503);

        var result = await _vrchat.Auth.LogoutAsync(CreateRequestToken(cancellationToken)).ConfigureAwait(false);
        return ModuleApiResponse.Json(ToResponse(result));
    }

    /// <summary>
    /// Loads application configuration and applies it to the VRChat integration before auth operations.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels integration configuration.</param>
    /// <returns><see langword="true"/> when configuration was available and applied.</returns>
    private async Task<bool> TryConfigureVRChatAsync(CancellationToken cancellationToken)
    {
        await _vrchat.ConfigureAsync(GetOptions(), cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Maps a VRChat auth operation result to the anonymous JSON shape used by module auth endpoints.
    /// </summary>
    /// <param name="result">VRChat operation result returned by the auth service.</param>
    /// <returns>An anonymous response object with success, message, error, and status fields.</returns>
    private static object ToResponse(VRChatOperationResult result)
        => new
        {
            success = result.Success,
            message = string.IsNullOrWhiteSpace(result.Message) ? null : result.Message,
            error = string.IsNullOrWhiteSpace(result.Error) ? null : result.Error,
            status = ToDto(result.Status)
        };

    /// <summary>
    /// Maps a nullable VRChat status into the module status DTO shape.
    /// </summary>
    /// <param name="status">VRChat status returned by the auth service.</param>
    /// <returns>A DTO with defaults when status is unavailable.</returns>
    private static object ToDto(VRChatStatus? status)
    {
        if (status is null)
            return new VRChatStatusDto();

        return new VRChatStatusDto
        {
            HasStoredSession = status.HasStoredSession,
            IsConnected = status.IsConnected,
            RequiresTwoFactor = status.RequiresTwoFactor,
            RequiresEmailCode = status.RequiresEmailCode,
            RequiresLoginPlaceVerification = status.RequiresLoginPlaceVerification,
            TwoFactorMethods = status.TwoFactorMethods is null ? [] : [.. status.TwoFactorMethods],
            CompletedTwoFactorMethods = status.CompletedTwoFactorMethods is null ? [] : [.. status.CompletedTwoFactorMethods],
            DisplayName = status.DisplayName,
            UserId = status.UserId,
            AccountLogin = status.AccountLogin,
            EmailHint = status.EmailHint,
            PendingEmailHint = status.PendingEmailHint,
            LastVerifiedUtc = status.LastVerifiedUtc,
            UpdatedAtUtc = status.UpdatedAtUtc,
            LastError = status.LastError,
            AutoReconnect = status.AutoReconnect
        };
    }

    /// <summary>
    /// Parses CRLF-separated HTTP header text into a case-insensitive dictionary, keeping the last duplicate value.
    /// </summary>
    /// <param name="headers">Raw header text returned by the VRChat auth service.</param>
    /// <returns>A case-insensitive dictionary of header names and values.</returns>
    private static IReadOnlyDictionary<string, string> ParseHeaders(string? headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
            return new Dictionary<string, string>();

        return headers
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .GroupBy(parts => parts[0].Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last()[1].Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
