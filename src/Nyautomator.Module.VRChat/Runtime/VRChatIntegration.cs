using Nyautomator;
using Nyautomator.Runtime.Abstractions;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Runtime integration that configures VRChat authentication, Dolly runtime settings, and session restoration.
/// </summary>
public sealed class VRChatIntegration : IVRChatIntegration
{
    private const string ModuleId = "vrchat";

    /// <summary>
    /// Authentication service that owns VRChat login state and authenticated API requests.
    /// </summary>
    private readonly VRChatAuthService _auth;

    private readonly IModuleOptionsProvider? _optionsProvider;
    private readonly IModuleDataPathProvider? _dataPathProvider;

    /// <summary>
    /// Tracks whether the integration has been disposed and should reject further lifecycle calls.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// Tracks whether configuration has been applied before startup attempts session restoration.
    /// </summary>
    private bool _isConfigured;

    /// <summary>
    /// Stores the configured auto-reconnect preference used by <see cref="StartAsync"/>.
    /// </summary>
    private bool _autoReconnect = true;

    /// <summary>
    /// Initializes a new instance of the VRChat integration and wires authentication logs to the integration event.
    /// </summary>
    public VRChatIntegration()
        : this(null, null, (IVRChatSessionStore?)null)
    {
    }

    public VRChatIntegration(
        IModuleOptionsProvider optionsProvider,
        IModuleDataPathProvider dataPathProvider,
        IIntegrationTokenStore tokens)
        : this(optionsProvider, dataPathProvider, new VRChatSessionStoreAdapter(tokens))
    {
    }

    private VRChatIntegration(
        IModuleOptionsProvider? optionsProvider,
        IModuleDataPathProvider? dataPathProvider,
        IVRChatSessionStore? sessionStore)
    {
        _optionsProvider = optionsProvider;
        _dataPathProvider = dataPathProvider;
        _auth = new VRChatAuthService(sessionStore);
        _auth.LogEmitted += ForwardLog;
        if (_optionsProvider is not null)
            _optionsProvider.ModuleOptionsChanged += OnModuleOptionsChanged;
    }

    /// <summary>
    /// Gets the runtime participant name reported to Nyautomator.
    /// </summary>
    public string Name => "VRChat";

    /// <summary>
    /// Gets the authentication service used by module APIs and authenticated integration adapters.
    /// </summary>
    public VRChatAuthService Auth => _auth;

    /// <summary>
    /// Occurs when the underlying authentication service emits a diagnostic log entry.
    /// </summary>
    public event Action<VRChatLogEntry>? LogEmitted;

    /// <summary>
    /// Applies application configuration to VRChat authentication and Dolly runtime services.
    /// </summary>
    /// <param name="config">Application configuration containing VRChat authentication and Dolly settings.</param>
    /// <param name="cancellationToken">Token supplied by the runtime; configuration completes synchronously.</param>
    /// <returns>A completed task after configuration is applied.</returns>
    public Task ConfigureAsync(AppConfiguration config, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ApplyOptions(GetConfiguredOptions());
        return Task.CompletedTask;
    }

    public Task ConfigureAsync(VRChatModuleOptions options, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ApplyOptions(options);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the integration by restoring a stored VRChat session when configured and auto-reconnect is enabled.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels startup session restoration.</param>
    /// <returns>A task that completes after the optional restore attempt.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (!_isConfigured || !_autoReconnect)
            return;

        try
        {
            await _auth.TryRestoreSessionAsync(forceRefresh: true, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown path.
        }
    }

    /// <summary>
    /// Stops the integration; VRChat currently has no runtime work to tear down here.
    /// </summary>
    /// <param name="cancellationToken">Token supplied by the runtime; shutdown completes synchronously.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Explicitly attempts to restore a stored VRChat session through the authentication service.
    /// </summary>
    /// <param name="forceRefresh">Whether the authentication service should refresh status after restoration.</param>
    /// <param name="cancellationToken">Token that cancels session restoration.</param>
    /// <returns>The latest VRChat authentication status after the restore attempt.</returns>
    public Task<VRChatStatus> RestoreSessionAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _auth.TryRestoreSessionAsync(forceRefresh, cancellationToken);
    }

    /// <summary>
    /// Detaches authentication log forwarding and marks the integration as disposed.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        if (_optionsProvider is not null)
            _optionsProvider.ModuleOptionsChanged -= OnModuleOptionsChanged;
        _auth.LogEmitted -= ForwardLog;
        return ValueTask.CompletedTask;
    }

    private void ApplyOptions(VRChatModuleOptions? options)
    {
        options ??= GetDefaultOptions();
        var dataDirectory = GetModuleDataDirectory();
        var defaultTrackDirectory = string.IsNullOrWhiteSpace(dataDirectory)
            ? null
            : Path.Combine(dataDirectory, "dolly", "tracks");

        _auth.Configure(options.Auth);
        VRChatDollyRuntime.Configure(options.Dolly, defaultTrackDirectory);
        _isConfigured = true;
        _autoReconnect = options.Auth.AutoReconnect ?? true;
    }

    private VRChatModuleOptions GetConfiguredOptions()
        => _optionsProvider?.Get(ModuleId, GetDefaultOptions()) ?? GetDefaultOptions();

    private VRChatModuleOptions GetDefaultOptions()
        => VRChatModuleOptions.CreateDefault(GetModuleDataDirectory());

    private string? GetModuleDataDirectory()
    {
        try
        {
            return _dataPathProvider?.GetModuleDataDirectory(ModuleId);
        }
        catch
        {
            return null;
        }
    }

    private void OnModuleOptionsChanged(ModuleOptionsChangedEventArgs args)
    {
        if (_disposed || !string.Equals(args.ModuleId, ModuleId, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            ApplyOptions(GetConfiguredOptions());
        }
        catch
        {
        }
    }

    /// <summary>
    /// Forwards authentication log entries to integration observers while isolating observer failures.
    /// </summary>
    /// <param name="entry">Log entry emitted by the authentication service.</param>
    private void ForwardLog(VRChatLogEntry entry)
    {
        try
        {
            LogEmitted?.Invoke(entry);
        }
        catch
        {
            // Ignore observer failures.
        }
    }

    /// <summary>
    /// Throws when a lifecycle or auth operation is invoked after disposal.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the integration has already been disposed.</exception>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(VRChatIntegration));
    }
}
