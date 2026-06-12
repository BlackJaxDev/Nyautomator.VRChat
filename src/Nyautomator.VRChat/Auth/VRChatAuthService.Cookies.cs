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
/// Cookie storage and restoration helpers for <see cref="VRChatAuthService"/>.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// Extracts a named cookie value from a raw value, Cookie header, or semicolon-separated cookie list.
    /// </summary>
    /// <param name="cookieOrHeader">Raw cookie value or header text.</param>
    /// <param name="cookieName">Cookie name to extract.</param>
    /// <returns>The extracted cookie value, or <see langword="null"/> when missing.</returns>
    private static string? ExtractCookieValue(string? cookieOrHeader, string cookieName)
    {
        var trimmed = cookieOrHeader?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (trimmed.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["Cookie:".Length..].Trim();

        if (!trimmed.Contains(';') && !trimmed.StartsWith(cookieName + "=", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        foreach (var part in trimmed.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = part.Split('=', 2);
            if (split.Length == 2 && string.Equals(split[0].Trim(), cookieName, StringComparison.OrdinalIgnoreCase))
                return split[1].Trim();
        }

        return null;
    }

    /// <summary>
    /// Copies auth cookies from a VRChat SDK response into SDK configuration and persisted metadata.
    /// </summary>
    /// <param name="cookies">Cookies returned by the generated VRChat API client.</param>
    private void UpdateCookies(IList<Cookie>? cookies)
    {
        if (cookies is not null)
        {
            foreach (var cookie in cookies)
            {
                if (cookie is null || string.IsNullOrWhiteSpace(cookie.Name))
                    continue;

                if (string.Equals(cookie.Name, "auth", StringComparison.OrdinalIgnoreCase))
                {
                    SetCookie("auth", cookie.Value);
                    SetMetadata(MetadataAuthCookie, cookie.Value);
                }
                else if (string.Equals(cookie.Name, "twoFactorAuth", StringComparison.OrdinalIgnoreCase))
                {
                    SetCookie("twoFactorAuth", cookie.Value);
                    SetMetadata(MetadataTwoFactorCookie, cookie.Value);
                }
            }
        }

        CaptureKnownCookiesFromApiClient();
    }

    /// <summary>
    /// Restores SDK and HTTP client cookies from persisted metadata.
    /// </summary>
    private void ApplyCookiesFromMetadata()
    {
        SetCookie("auth", GetMetadata(MetadataAuthCookie));
        SetCookie("twoFactorAuth", GetMetadata(MetadataTwoFactorCookie));
    }

    /// <summary>
    /// Adds, updates, or removes a cookie in SDK API keys and the HTTP cookie container.
    /// </summary>
    /// <param name="name">VRChat cookie name.</param>
    /// <param name="value">Cookie value, or blank to remove and expire the cookie.</param>
    private void SetCookie(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (_configuration.ApiKey.ContainsKey(name))
                _configuration.ApiKey.Remove(name);
            ExpireApiClientCookie(name);
            return;
        }

        _configuration.AddApiKey(name, value);
        SetApiClientCookie(name, value);
    }

    /// <summary>
    /// Adds a non-expired cookie to the generated API client's cookie container.
    /// </summary>
    /// <param name="name">VRChat cookie name.</param>
    /// <param name="value">Cookie value to store.</param>
    private void SetApiClientCookie(string name, string value)
    {
        var cookie = new Cookie(name, value, "/", "api.vrchat.cloud");
        _httpClientHandler.CookieContainer.Add(new Uri(DefaultBasePath), cookie);
    }

    /// <summary>
    /// Adds an expired cookie to the generated API client's cookie container.
    /// </summary>
    /// <param name="name">VRChat cookie name to expire.</param>
    private void ExpireApiClientCookie(string name)
    {
        var cookie = new Cookie(name, string.Empty, "/", "api.vrchat.cloud")
        {
            Expires = DateTime.UtcNow.AddDays(-1)
        };
        _httpClientHandler.CookieContainer.Add(new Uri(DefaultBasePath), cookie);
    }

    /// <summary>
    /// Reads known VRChat cookies already stored in the HTTP client's cookie container back into metadata.
    /// </summary>
    private void CaptureKnownCookiesFromApiClient()
    {
        foreach (Cookie cookie in _httpClientHandler.CookieContainer.GetAllCookies())
        {
            if (string.Equals(cookie.Name, "auth", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(cookie.Value)
                && !cookie.Expired)
            {
                _configuration.AddApiKey("auth", cookie.Value);
                SetMetadata(MetadataAuthCookie, cookie.Value);
            }
            else if (string.Equals(cookie.Name, "twoFactorAuth", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(cookie.Value)
                && !cookie.Expired)
            {
                _configuration.AddApiKey("twoFactorAuth", cookie.Value);
                SetMetadata(MetadataTwoFactorCookie, cookie.Value);
            }
        }
    }

    /// <summary>
    /// Checks whether an auth cookie exists either in persisted metadata or the API client's cookie container.
    /// </summary>
    /// <returns><see langword="true"/> when an auth cookie is available.</returns>
    private bool HasPendingAuthCookie()
        => !string.IsNullOrWhiteSpace(GetMetadata(MetadataAuthCookie))
           || HasApiClientCookie("auth");

    /// <summary>
    /// Checks whether the API client's cookie container has a non-expired cookie with the given name.
    /// </summary>
    /// <param name="name">Cookie name to search for.</param>
    /// <returns><see langword="true"/> when a matching non-expired cookie exists.</returns>
    private bool HasApiClientCookie(string name)
    {
        foreach (Cookie cookie in _httpClientHandler.CookieContainer.GetAllCookies())
        {
            if (string.Equals(cookie.Name, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(cookie.Value)
                && !cookie.Expired)
            {
                return true;
            }
        }

        return false;
    }
}
