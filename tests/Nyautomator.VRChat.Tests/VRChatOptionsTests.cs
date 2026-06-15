using System.Text;
using System.Text.Json;
using Nyautomator;
using Nyautomator.Module.Abstractions;
using Nyautomator.Modules.VRChat;
using Nyautomator.Runtime.Abstractions;
using Xunit;

namespace Nyautomator.VRChat.Tests;

public sealed class VRChatOptionsTests
{
    [Fact]
    public void CreateDefault_UsesModuleDataDirectoryForDollyTracks()
    {
        using var temp = new TempDirectory();

        var options = VRChatModuleOptions.CreateDefault(temp.Path);

        Assert.Equal(Path.Combine(temp.Path, "dolly", "tracks"), options.Dolly.TrackDirectory);
    }

    [Fact]
    public void DollyRuntime_ConfigureMapsDollyOptionsAndDefaultDirectory()
    {
        using var temp = new TempDirectory();
        var defaultTrackDirectory = Path.Combine(temp.Path, "default-tracks");

        VRChatDollyRuntime.Configure(new VRChatDollyOptionsInput
        {
            Enabled = false,
            TrackDirectory = null,
            DefaultFrameRate = 240,
            PoseFreshnessMilliseconds = 1,
            SettingsFreshnessMilliseconds = 1
        }, defaultTrackDirectory);

        var status = VRChatDollyRuntime.GetStatus();

        Assert.False(status.Enabled);
        Assert.Equal(Path.GetFullPath(defaultTrackDirectory), status.TrackDirectory);
    }

    [Fact]
    public async Task ModuleBridge_UsesConfiguredOscPortsForPassthrough()
    {
        VRChatHelper.OSC.ConfigurePassthrough(false, 9010, 9011);
        var options = new TestModuleOptionsProvider(new VRChatModuleOptions
        {
            Osc = new VRChatOscOptions
            {
                Enabled = false,
                PassthroughInputPort = 19110,
                PassthroughOutputPort = 19111
            }
        });
        using var temp = new TempDirectory();
        var bridge = new VRChatModuleBridge(
            options,
            new TestModuleDataPathProvider(temp.Path),
            new TestIntegration());
        using var body = JsonBody(new { enabled = true });

        var response = await bridge.HandleAsync(new ModuleApiRequest(
            "vrchat",
            "POST",
            "osc/passthrough",
            new Dictionary<string, string?>(),
            body,
            "application/json",
            new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.False(VRChatHelper.OSC.PassthroughEnabled);
        Assert.Equal(19110, VRChatHelper.OSC.ExternalInputPort);
        Assert.Equal(19111, VRChatHelper.OSC.ExternalOutputPort);
    }

    [Fact]
    public async Task Integration_MapsAuthOptionsOnConfigure()
    {
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            AccountId = "usr_123",
            AccountLogin = "login@example.test",
            AccountDisplayName = "Display",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["UserId"] = "usr_123",
                ["DisplayName"] = "Display",
                ["AccountLogin"] = "login@example.test"
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var tokens = new TestTokenStore(store);
        using var temp = new TempDirectory();
        await using var integration = new VRChatIntegration(
            new TestModuleOptionsProvider(new VRChatModuleOptions
            {
                Auth = new VRChatAuthOptions { AutoReconnect = false, CookieTtlDays = 30 },
                Dolly = new VRChatDollyOptionsInput { Enabled = true }
            }),
            new TestModuleDataPathProvider(temp.Path),
            tokens);

        await integration.ConfigureAsync(new AppConfiguration(), CancellationToken.None);
        var status = await integration.RestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.False(status.AutoReconnect);
        Assert.True(status.IsConnected);
    }

    private static MemoryStream JsonBody(object value)
        => new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

    private sealed class TestModuleOptionsProvider(VRChatModuleOptions options) : IModuleOptionsProvider
    {
        private readonly VRChatModuleOptions _options = options;

        public event Action<ModuleOptionsChangedEventArgs>? ModuleOptionsChanged;

        public T Get<T>(string moduleId) where T : new()
            => Get(moduleId, new T());

        public T Get<T>(string moduleId, T defaults)
            => _options is T typed ? typed : defaults;

        public bool TryGetRaw(string moduleId, out JsonElement rawOptions)
        {
            rawOptions = JsonSerializer.SerializeToElement(_options, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return true;
        }

        public bool Write<T>(string moduleId, T options, bool force = true)
        {
            ModuleOptionsChanged?.Invoke(new ModuleOptionsChangedEventArgs(moduleId, JsonSerializer.SerializeToElement(options)));
            return true;
        }
    }

    private sealed class TestModuleDataPathProvider(string root) : IModuleDataPathProvider
    {
        public string GetModuleDataDirectory(string moduleId)
            => Path.Combine(root, moduleId);

        public string GetModuleDataFilePath(string moduleId, string relativePath)
            => Path.Combine(GetModuleDataDirectory(moduleId), relativePath);
    }

    private sealed class TestIntegration : IVRChatIntegration
    {
        public string Name => "VRChat";
        public VRChatAuthService Auth { get; } = new(new FakeSessionStore(null));
        public event Action<VRChatLogEntry>? LogEmitted;

        public Task ConfigureAsync(AppConfiguration config, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task ConfigureAsync(VRChatModuleOptions options, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<VRChatStatus> RestoreSessionAsync(bool forceRefresh, CancellationToken cancellationToken)
            => Auth.TryRestoreSessionAsync(forceRefresh, cancellationToken);

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class FakeSessionStore(VRChatSessionToken? token) : IVRChatSessionStore
    {
        private VRChatSessionToken? _token = token?.Clone();

        public VRChatSessionToken? Get()
            => _token?.Clone();

        public void Set(VRChatSessionToken token)
            => _token = token.Clone();

        public void Clear()
            => _token = null;
    }

    private sealed class TestTokenStore(FakeSessionStore store) : IIntegrationTokenStore
    {
        public IntegrationToken? Get(string key)
        {
            var token = store.Get();
            return token is null
                ? null
                : new IntegrationToken
                {
                    AccountId = token.AccountId,
                    AccountLogin = token.AccountLogin,
                    AccountDisplayName = token.AccountDisplayName,
                    Metadata = token.Metadata is null
                        ? null
                        : new Dictionary<string, string>(token.Metadata, StringComparer.OrdinalIgnoreCase),
                    UpdatedAtUtc = token.UpdatedAtUtc
                };
        }

        public IReadOnlyDictionary<string, IntegrationToken> Snapshot()
            => new Dictionary<string, IntegrationToken>();

        public void Set(string key, IntegrationToken token)
        {
            store.Set(new VRChatSessionToken
            {
                AccountId = token.AccountId,
                AccountLogin = token.AccountLogin,
                AccountDisplayName = token.AccountDisplayName,
                Metadata = token.Metadata is null
                    ? null
                    : new Dictionary<string, string>(token.Metadata, StringComparer.OrdinalIgnoreCase),
                UpdatedAtUtc = token.UpdatedAtUtc
            });
        }

        public void Clear(string key)
            => store.Clear();
    }
}
