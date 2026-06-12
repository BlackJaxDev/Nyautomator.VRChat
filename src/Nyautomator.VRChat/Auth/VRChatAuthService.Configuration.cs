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
/// Configuration helpers for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Stores a new option snapshot and rebuilds SDK configuration values that depend on it.
    /// </summary>
    /// <param name="snapshot">Configuration options derived from app settings.</param>
    private void UpdateOptions(OptionsSnapshot snapshot)
    {
        _options = snapshot;
        ApplyBasePath();
        ApplyUserAgent();
        _authenticationApi = CreateAuthenticationApi();
    }

    /// <summary>
    /// Creates the generated VRChat authentication API wrapper over the shared API client and configuration.
    /// </summary>
    /// <returns>A generated authentication API instance.</returns>
    private AuthenticationApi CreateAuthenticationApi()
        => new(_apiClient, _apiClient, _configuration);

    /// <summary>
    /// Applies the default Nyautomator user agent to both generated SDK and raw HTTP clients.
    /// </summary>
    private void ApplyUserAgent()
    {
        _configuration.UserAgent = DefaultUserAgent;
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(DefaultUserAgent);
    }

    /// <summary>
    /// Resets the generated SDK base path to VRChat's API endpoint.
    /// </summary>
    private void ApplyBasePath()
    {
        _configuration.BasePath = DefaultBasePath;
    }

    /// <summary>
    /// Reads the configured cookie lifetime and falls back to seven days when unset or invalid.
    /// </summary>
    /// <param name="options">VRChat auth options.</param>
    /// <returns>Positive cookie lifetime in days.</returns>
    private static int GetCookieTtl(VRChatAuthOptions? options)
    {
        if (options?.CookieTtlDays is int days && days > 0)
            return days;
        return 7;
    }

    /// <summary>
    /// Reads the configured auto-reconnect flag and defaults to enabled.
    /// </summary>
    /// <param name="options">VRChat auth options.</param>
    /// <returns><see langword="true"/> when stored sessions should be restored on startup.</returns>
    private static bool GetAutoReconnect(VRChatAuthOptions? options)
        => options?.AutoReconnect ?? true;
}
