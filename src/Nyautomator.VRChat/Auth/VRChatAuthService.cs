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
/// Provides VRChat authentication, stored-session management, verification state, and authenticated API forwarding.
/// </summary>
public sealed partial class VRChatAuthService
{
    /// <summary>
    /// User-Agent sent to VRChat cloud API requests.
    /// </summary>
    private const string DefaultUserAgent = "Nyautomator.VRChat/1.0";

    /// <summary>
    /// Base URL for the VRChat public API.
    /// </summary>
    private const string DefaultBasePath = "https://api.vrchat.cloud/api/1";

    /// <summary>
    /// Metadata key containing the stored VRChat auth cookie.
    /// </summary>
    private const string MetadataAuthCookie = "AuthCookie";

    /// <summary>
    /// Metadata key containing the stored VRChat two-factor cookie.
    /// </summary>
    private const string MetadataTwoFactorCookie = "TwoFactorAuthCookie";

    /// <summary>
    /// Metadata key containing the authenticated VRChat user id.
    /// </summary>
    private const string MetadataUserId = "UserId";

    /// <summary>
    /// Metadata key containing the authenticated VRChat display name.
    /// </summary>
    private const string MetadataDisplayName = "DisplayName";

    /// <summary>
    /// Metadata key containing the account login submitted for authentication.
    /// </summary>
    private const string MetadataAccountLogin = "AccountLogin";

    /// <summary>
    /// Metadata key containing the account email hint reported by VRChat.
    /// </summary>
    private const string MetadataEmailHint = "EmailHint";

    /// <summary>
    /// Metadata key containing the email hint for a pending verification challenge.
    /// </summary>
    private const string MetadataPendingEmailHint = "PendingEmailHint";

    /// <summary>
    /// Metadata key containing the UTC timestamp of the last fully verified session.
    /// </summary>
    private const string MetadataLastVerifiedUtc = "LastVerifiedUtc";

    /// <summary>
    /// Metadata key indicating that an email one-time code is pending.
    /// </summary>
    private const string MetadataRequiresEmail2Fa = "RequiresEmail2FA";

    /// <summary>
    /// Metadata key indicating that a two-factor verification step is pending.
    /// </summary>
    private const string MetadataPendingTwoFactor = "PendingTwoFactor";

    /// <summary>
    /// Metadata key containing comma-separated pending two-factor method names.
    /// </summary>
    private const string MetadataTwoFactorMethods = "TwoFactorMethods";

    /// <summary>
    /// Metadata key containing comma-separated two-factor methods completed in the current login flow.
    /// </summary>
    private const string MetadataCompletedTwoFactorMethods = "CompletedTwoFactorMethods";

    /// <summary>
    /// Metadata key for legacy login-place verification state.
    /// </summary>
    private const string MetadataRequiresLoginPlaceVerification = "RequiresLoginPlaceVerification";

    /// <summary>
    /// Maximum attempts used by retry helpers for rate-limited or transient VRChat API calls.
    /// </summary>
    private const int RateLimitMaxAttempts = 5;

    /// <summary>
    /// Initial backoff used for transient VRChat API retries.
    /// </summary>
    private static readonly TimeSpan RateLimitInitialDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum backoff used for transient VRChat API retries.
    /// </summary>
    private static readonly TimeSpan RateLimitMaxDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cache lifetime for current-user refreshes when a forced refresh is not requested.
    /// </summary>
    private static readonly TimeSpan CurrentUserCacheDuration = TimeSpan.FromSeconds(30);

    // Conservative sliding-window ceiling for all VRChat API traffic (matches VRCX's bulk guardrail).
    /// <summary>
    /// Conservative per-minute request ceiling shared by all VRChat API calls.
    /// </summary>
    private const int RateLimitRequestsPerMinute = 60;

    /// <summary>
    /// Cooldown applied after repeated requests to an endpoint return forbidden or not-found.
    /// </summary>
    private static readonly TimeSpan FailedRequestCooldown = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Serializes mutation of authentication cookies, metadata, and verification state.
    /// </summary>
    private readonly SemaphoreSlim _sync = new(1, 1);

    /// <summary>
    /// Shared sliding-window limiter for VRChat API requests made by this auth service.
    /// </summary>
    private readonly VRChatRateLimiter _rateLimiter = new(RateLimitRequestsPerMinute, TimeSpan.FromMinutes(1));

    /// <summary>
    /// Tracks temporarily cooled-down request keys after recent 403 or 404 responses.
    /// </summary>
    private readonly Dictionary<string, DateTime> _failedRequests = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// HTTP handler that stores VRChat cookies for SDK-backed requests.
    /// </summary>
    private readonly HttpClientHandler _httpClientHandler;

    /// <summary>
    /// HTTP client supplied to the generated VRChat API client.
    /// </summary>
    private readonly HttpClient _httpClient;

    private readonly IVRChatSessionStore _sessionStore;

    /// <summary>
    /// Generated VRChat API client shared by authentication endpoints.
    /// </summary>
    private readonly ApiClient _apiClient;

    /// <summary>
    /// Generated VRChat SDK configuration carrying base path, timeout, user agent, credentials, and API keys.
    /// </summary>
    private readonly Configuration _configuration;

    /// <summary>
    /// Generated authentication API wrapper rebuilt when configuration changes.
    /// </summary>
    private AuthenticationApi _authenticationApi;

    /// <summary>
    /// Persistable session metadata copied to and from the integration token store.
    /// </summary>
    private Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Pending two-factor method names reported by VRChat for the current login flow.
    /// </summary>
    private List<string> _twoFactorMethods = [];

    /// <summary>
    /// Two-factor methods already completed during the current login flow.
    /// </summary>
    private List<string> _completedTwoFactorMethods = [];

    /// <summary>
    /// Current auth options derived from app configuration.
    /// </summary>
    private OptionsSnapshot _options = new(7, true);

    /// <summary>
    /// Cached current user returned by the VRChat API when fully authenticated.
    /// </summary>
    private CurrentUser? _currentUser;

    /// <summary>
    /// Cached authenticated VRChat user id, preserved even when the current user object is not available.
    /// </summary>
    private string? _cachedUserId;

    /// <summary>
    /// Cached authenticated VRChat display name.
    /// </summary>
    private string? _cachedDisplayName;

    /// <summary>
    /// Cached account login submitted by the user.
    /// </summary>
    private string? _cachedLogin;

    /// <summary>
    /// Cached account email hint from VRChat.
    /// </summary>
    private string? _cachedEmailHint;

    /// <summary>
    /// Cached pending-verification email hint from VRChat.
    /// </summary>
    private string? _cachedPendingEmailHint;

    /// <summary>
    /// UTC time when the session was last fully verified.
    /// </summary>
    private DateTime? _lastVerifiedUtc;

    /// <summary>
    /// UTC time when persisted session metadata was last written.
    /// </summary>
    private DateTime? _updatedAtUtc;

    /// <summary>
    /// UTC time when the current-user endpoint was last refreshed successfully.
    /// </summary>
    private DateTime? _lastCurrentUserFetchUtc;

    /// <summary>
    /// Tracks whether a two-factor verification step is pending.
    /// </summary>
    private bool _requiresTwoFactor;

    /// <summary>
    /// Tracks whether the pending verification step is an email one-time code.
    /// </summary>
    private bool _requiresEmailCode;

    /// <summary>
    /// Tracks legacy login-place verification state for restored sessions.
    /// </summary>
    private bool _requiresLoginPlaceVerification;

    /// <summary>
    /// Stores the last user-facing auth error.
    /// </summary>
    private string? _lastError;

    /// <summary>
    /// Immutable auth option snapshot used to avoid retaining the full app configuration.
    /// </summary>
    /// <param name="CookieTtlDays">Number of days stored cookies are considered valid.</param>
    /// <param name="AutoReconnect">Whether startup should attempt stored-session restoration.</param>
    private record struct OptionsSnapshot(int CookieTtlDays, bool AutoReconnect);

    /// <summary>
    /// Occurs when the authentication service emits a diagnostic log entry.
    /// </summary>
    public event Action<VRChatLogEntry>? LogEmitted;

    /// <summary>
    /// Initializes HTTP, SDK, and authentication API clients with default VRChat settings.
    /// </summary>
    public VRChatAuthService(IVRChatSessionStore? sessionStore = null)
    {
        _sessionStore = sessionStore ?? new VRChatFileSessionStore();
        _httpClientHandler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true
        };
        _httpClient = new HttpClient(_httpClientHandler, disposeHandler: false);
        _apiClient = new ApiClient(_httpClient, DefaultBasePath, _httpClientHandler);
        _configuration = new Configuration
        {
            BasePath = DefaultBasePath,
            Timeout = TimeSpan.FromSeconds(30)
        };
        ApplyUserAgent();
        _authenticationApi = CreateAuthenticationApi();
    }

    /// <summary>
    /// Emits a diagnostic log entry and isolates observer exceptions from the auth pipeline.
    /// </summary>
    /// <param name="level">Log severity label.</param>
    /// <param name="eventName">Stable auth event name.</param>
    /// <param name="message">Human-readable log message.</param>
    /// <param name="detail">Optional diagnostic detail.</param>
    private void EmitLog(string level, string eventName, string message, string? detail = null)
    {
        var entry = new VRChatLogEntry(DateTime.UtcNow, level, eventName, message, detail);
        try
        {
            LogEmitted?.Invoke(entry);
        }
        catch
        {
            // Intentionally swallow to avoid propagating observer failures back into auth pipeline.
        }
    }

    /// <summary>
    /// Emits an informational authentication log entry.
    /// </summary>
    /// <param name="eventName">Stable auth event name.</param>
    /// <param name="message">Human-readable log message.</param>
    /// <param name="detail">Optional diagnostic detail.</param>
    private void EmitInfo(string eventName, string message, string? detail = null)
        => EmitLog("Information", eventName, message, detail);

    /// <summary>
    /// Emits a warning authentication log entry.
    /// </summary>
    /// <param name="eventName">Stable auth event name.</param>
    /// <param name="message">Human-readable log message.</param>
    /// <param name="detail">Optional diagnostic detail.</param>
    private void EmitWarning(string eventName, string message, string? detail = null)
        => EmitLog("Warning", eventName, message, detail);

    /// <summary>
    /// Emits an error authentication log entry.
    /// </summary>
    /// <param name="eventName">Stable auth event name.</param>
    /// <param name="message">Human-readable log message.</param>
    /// <param name="detail">Optional diagnostic detail.</param>
    private void EmitError(string eventName, string message, string? detail = null)
        => EmitLog("Error", eventName, message, detail);

    /// <summary>
    /// Applies module-owned VRChat auth options.
    /// </summary>
    /// <param name="options">VRChat authentication options.</param>
    public void Configure(VRChatAuthOptions? options)
    {
        var snapshot = new OptionsSnapshot(
            GetCookieTtl(options),
            GetAutoReconnect(options));

        UpdateOptions(snapshot);
    }
}
