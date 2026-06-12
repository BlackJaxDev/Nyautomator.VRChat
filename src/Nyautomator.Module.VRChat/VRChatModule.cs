using Microsoft.Extensions.DependencyInjection;
using Nyautomator;
using Nyautomator.Module.Abstractions;
using Nyautomator.Runtime.Abstractions;
using NyautomatorUI.Server.Automation;
using System.Text.Json;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Nyautomator module entry point that registers VRChat services, API handlers, and authenticated integration adapters.
/// </summary>
public sealed class VRChatModule : INyautomatorModule
{
    /// <summary>
    /// Synchronizes fallback integration and bridge creation when dependency injection has not provided them.
    /// </summary>
    private static readonly object FallbackGate = new();

    /// <summary>
    /// Stores the process-wide fallback VRChat integration used outside the normal service registration path.
    /// </summary>
    private static IVRChatIntegration? _fallbackIntegration;

    /// <summary>
    /// Stores the process-wide fallback module bridge used when the service provider does not contain one.
    /// </summary>
    private static VRChatModuleBridge? _fallbackBridge;

    /// <summary>
    /// Gets the module metadata advertised to the Nyautomator module loader.
    /// </summary>
    public NyautomatorModuleDescriptor Descriptor { get; } = new(
        "vrchat",
        "VRChat",
        "VRChat OSC, cloud API, avatar, and Dolly integration.",
        "0.1.0");

    /// <summary>
    /// Registers the VRChat integration services and the module bridge with the host service collection.
    /// </summary>
    /// <param name="context">Module service configuration context supplied by the host.</param>
    /// <param name="services">Service collection receiving the VRChat registrations.</param>
    public void ConfigureServices(NyautomatorModuleServiceContext context, IServiceCollection services)
    {
        services.AddNyautomatorVRChatIntegration();
        services.AddSingleton<VRChatModuleBridge>();
    }

    /// <summary>
    /// Registers automation event integration, module API handling, and authenticated request adapters at runtime.
    /// </summary>
    /// <param name="context">Runtime configuration context supplied by the module host.</param>
    public void ConfigureRuntime(NyautomatorModuleRuntimeContext context)
    {
        VRChatAutomationIntegration.Register();

        var defaults = JsonSerializer.SerializeToElement(
            VRChatModuleOptions.CreateDefault(
                context.Services.GetService<IModuleDataPathProvider>()?.GetModuleDataDirectory(context.ModuleId)),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var configuration = new ModuleConfigurationContribution(
            "options",
            Defaults: defaults,
            ModuleId: context.ModuleId);
        context.Configurations.Register(configuration);
        context.Contributions.Register(new ModuleContributionSet(context.ModuleId, Configurations: [configuration]));

        var bridge = context.Services.GetService<VRChatModuleBridge>() ?? GetFallbackBridge(context.Services);
        context.ApiHandlers.Register(bridge);
        context.AuthenticatedIntegrations.Register(bridge);
        context.AuthenticatedIntegrations.Register(new AuthenticatedIntegrationAliasAdapter("VRC", bridge));
    }

    /// <summary>
    /// Gets the shared VRChat integration from services, or creates a fallback integration once per process.
    /// </summary>
    /// <param name="services">Service provider used to resolve an existing integration.</param>
    /// <returns>The integration instance used by the module bridge and runtime participant.</returns>
    internal static IVRChatIntegration GetOrCreateIntegration(IServiceProvider services)
    {
        lock (FallbackGate)
        {
            _fallbackIntegration ??= services.GetService<IVRChatIntegration>() ?? new VRChatIntegration();
            return _fallbackIntegration;
        }
    }

    /// <summary>
    /// Gets the shared module bridge from services, or constructs a fallback bridge with the shared integration.
    /// </summary>
    /// <param name="services">Service provider used to resolve dependencies for the bridge.</param>
    /// <returns>The bridge instance used for module API and authenticated integration calls.</returns>
    private static VRChatModuleBridge GetFallbackBridge(IServiceProvider services)
    {
        lock (FallbackGate)
        {
            if (_fallbackBridge is not null)
                return _fallbackBridge;

            var options = services.GetRequiredService<IModuleOptionsProvider>();
            var dataPaths = services.GetRequiredService<IModuleDataPathProvider>();
            _fallbackBridge = new VRChatModuleBridge(options, dataPaths, GetOrCreateIntegration(services));
            return _fallbackBridge;
        }
    }

    /// <summary>
    /// Authenticated integration adapter that forwards requests to another adapter under an alternate id.
    /// </summary>
    /// <param name="id">Alias id exposed to the authenticated integration registry.</param>
    /// <param name="inner">Adapter that handles the actual request forwarding.</param>
    private sealed class AuthenticatedIntegrationAliasAdapter(string id, IAuthenticatedIntegrationAdapter inner)
        : IAuthenticatedIntegrationAdapter
    {
        /// <summary>
        /// Gets the alias id used by callers to select this adapter.
        /// </summary>
        public string Id { get; } = id;

        /// <summary>
        /// Forwards an authenticated integration request to the wrapped adapter.
        /// </summary>
        /// <param name="request">Request to send through the wrapped adapter.</param>
        /// <param name="cancellationToken">Token that cancels the forwarding operation.</param>
        /// <returns>The response returned by the wrapped adapter.</returns>
        public Task<Nyautomator.Module.Abstractions.AuthenticatedIntegrationResponse> SendAsync(
            Nyautomator.Module.Abstractions.AuthenticatedIntegrationRequest request,
            CancellationToken cancellationToken)
            => inner.SendAsync(request, cancellationToken);
    }
}
