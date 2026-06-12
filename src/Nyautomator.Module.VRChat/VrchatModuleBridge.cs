using System.Text.Json;
using Nyautomator.Module.Abstractions;
using Nyautomator.Runtime.Abstractions;
using NyautomatorUI.Server.Automation;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Bridges the VRChat module API and authenticated integration adapter contracts to the VRChat runtime services.
/// </summary>
/// <remarks>
/// Initializes a new instance of the bridge with configuration and VRChat integration dependencies.
/// </remarks>
/// <param name="options">Module options provider used for runtime settings.</param>
/// <param name="dataPaths">Module data path provider used for default storage paths.</param>
/// <param name="vrchat">VRChat integration used for authentication and cloud API access.</param>
public sealed partial class VRChatModuleBridge(
    IModuleOptionsProvider options,
    IModuleDataPathProvider dataPaths,
    IVRChatIntegration vrchat) : IModuleApiHandler, IAuthenticatedIntegrationAdapter
{
    /// <summary>
    /// Default timeout applied to VRChat authentication operations initiated by module API endpoints.
    /// </summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// JSON serializer settings used for module request bodies and server-sent event payloads.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Module options provider used to load VRChat and OSC runtime settings.
    /// </summary>
    private readonly IModuleOptionsProvider _options = options;

    private readonly IModuleDataPathProvider _dataPaths = dataPaths;

    /// <summary>
    /// VRChat integration that owns authentication and authenticated cloud API calls.
    /// </summary>
    private readonly IVRChatIntegration _vrchat = vrchat;

    /// <summary>
    /// Gets the module API id handled by this bridge.
    /// </summary>
    public string ModuleId => "vrchat";

    /// <summary>
    /// Gets the authenticated integration id used by automation callers.
    /// </summary>
    public string Id => "VRChat";

    private VRChatModuleOptions GetOptions()
        => _options.Get(ModuleId, VRChatModuleOptions.CreateDefault(GetModuleDataDirectory()));

    private string? GetModuleDataDirectory()
    {
        try
        {
            return _dataPaths.GetModuleDataDirectory(ModuleId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Routes a module API request to VRChat auth, OSC, or dolly handlers and converts failures to module responses.
    /// </summary>
    /// <param name="request">Module API request to handle.</param>
    /// <param name="cancellationToken">Token that cancels request handling.</param>
    /// <returns>A module API response for the requested VRChat path.</returns>
    public async Task<ModuleApiResponse> HandleAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var path = NormalizePath(request.Path);
        try
        {
            if (path.StartsWith("dolly/", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "dolly", StringComparison.OrdinalIgnoreCase))
                return await HandleDollyAsync(request, path, cancellationToken).ConfigureAwait(false);

            if (path.StartsWith("osc/", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "osc", StringComparison.OrdinalIgnoreCase))
                return await HandleOscAsync(request, path, cancellationToken).ConfigureAwait(false);

            return path.ToLowerInvariant() switch
            {
                "status" when IsGet(request) => await GetStatusAsync(request, cancellationToken).ConfigureAwait(false),
                "login" when IsPost(request) => await LoginAsync(request, cancellationToken).ConfigureAwait(false),
                "verify-totp" when IsPost(request) => await VerifyTotpAsync(request, cancellationToken).ConfigureAwait(false),
                "verify-email" when IsPost(request) => await VerifyEmailAsync(request, cancellationToken).ConfigureAwait(false),
                "verify-login-place" when IsPost(request) => await VerifyLoginPlaceAsync(cancellationToken).ConfigureAwait(false),
                "verify-login-place-token" when IsPost(request) => await VerifyLoginPlaceTokenAsync(request, cancellationToken).ConfigureAwait(false),
                "import-session" when IsPost(request) => await ImportSessionAsync(request, cancellationToken).ConfigureAwait(false),
                "resend-email" when IsPost(request) => await ResendEmailAsync(request, cancellationToken).ConfigureAwait(false),
                "logout" when IsPost(request) => await LogoutAsync(cancellationToken).ConfigureAwait(false),
                _ => NotFound($"VRChat module API path '/{path}' is not available.")
            };
        }
        catch (OperationCanceledException)
        {
            return Error("VRChat request cancelled or timed out.", 504);
        }
        catch (Exception ex)
        {
            return Error($"VRChat request failed: {ex.Message}", 500);
        }
    }
}
