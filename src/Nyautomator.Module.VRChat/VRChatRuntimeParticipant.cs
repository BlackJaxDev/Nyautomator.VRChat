using Nyautomator;
using Nyautomator.Runtime.Abstractions;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Runtime participant wrapper that exposes the shared VRChat integration to the Nyautomator runtime.
/// </summary>
/// <param name="services">Service provider used to resolve or create the shared VRChat integration.</param>
public sealed class VRChatRuntimeParticipant(IServiceProvider services) : IRuntimeParticipant
{
    /// <summary>
    /// Integration instance that performs the actual VRChat runtime lifecycle work.
    /// </summary>
    private readonly IVRChatIntegration _inner = VRChatModule.GetOrCreateIntegration(services);

    /// <summary>
    /// Gets the participant name from the underlying VRChat integration.
    /// </summary>
    public string Name => _inner.Name;

    /// <summary>
    /// Applies application configuration to the underlying VRChat integration.
    /// </summary>
    /// <param name="config">Application configuration supplied by the runtime.</param>
    /// <param name="cancellationToken">Token that cancels configuration.</param>
    /// <returns>A task that completes after configuration is applied.</returns>
    public Task ConfigureAsync(AppConfiguration config, CancellationToken cancellationToken)
        => _inner.ConfigureAsync(config, cancellationToken);

    /// <summary>
    /// Starts the underlying VRChat integration runtime work.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels startup.</param>
    /// <returns>A task that completes after startup.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
        => _inner.StartAsync(cancellationToken);

    /// <summary>
    /// Stops the underlying VRChat integration runtime work.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels shutdown.</param>
    /// <returns>A task that completes after shutdown.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
        => _inner.StopAsync(cancellationToken);

    /// <summary>
    /// Completes disposal without owning the shared VRChat integration instance.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
