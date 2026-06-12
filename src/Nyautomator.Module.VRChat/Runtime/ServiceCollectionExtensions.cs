using Microsoft.Extensions.DependencyInjection;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Service collection extensions for registering the VRChat runtime integration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the VRChat runtime integration as the singleton <see cref="IVRChatIntegration"/> service.
    /// </summary>
    /// <param name="services">Service collection that receives the VRChat integration registration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddNyautomatorVRChatIntegration(this IServiceCollection services)
    {
        services.AddSingleton<IVRChatIntegration, VRChatIntegration>();
        return services;
    }
}
