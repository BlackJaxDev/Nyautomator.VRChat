using System.Globalization;
using Nyautomator;
using Xunit;

namespace Nyautomator.VRChat.Tests;

public sealed class VRChatAuthSessionTests
{
    [Fact]
    public void FileSessionStore_Get_ReturnsNullWhenFileIsMissing()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "missing", "session.json");
        var store = new VRChatFileSessionStore(path);

        Assert.Null(store.Get());
    }

    [Fact]
    public void FileSessionStore_Get_ReturnsNullWhenJsonIsInvalid()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "session.json");
        File.WriteAllText(path, "{ definitely not json");
        var store = new VRChatFileSessionStore(path);

        Assert.Null(store.Get());
    }

    [Fact]
    public void FileSessionStore_SetGetClear_RoundTripsClonedToken()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "session.json");
        var store = new VRChatFileSessionStore(path);
        var token = new VRChatSessionToken
        {
            AccountId = "usr_123",
            AccountLogin = "login@example.test",
            AccountDisplayName = "Display",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["UserId"] = "usr_123"
            },
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        };

        store.Set(token);
        token.Metadata["AuthCookie"] = "mutated";

        var restored = store.Get();

        Assert.NotNull(restored);
        Assert.Equal("usr_123", restored.AccountId);
        Assert.Equal("login@example.test", restored.AccountLogin);
        Assert.Equal("Display", restored.AccountDisplayName);
        Assert.Equal("auth-cookie", restored.Metadata!["AuthCookie"]);
        Assert.True(restored.UpdatedAtUtc.HasValue);

        restored.Metadata["AuthCookie"] = "mutated-again";
        Assert.Equal("auth-cookie", store.Get()!.Metadata!["AuthCookie"]);

        store.Clear();

        Assert.Null(store.Get());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void FileSessionStore_Set_CreatesParentDirectoryAndStampsUpdatedAtUtc()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "nested", "session.json");
        var store = new VRChatFileSessionStore(path);
        var suppliedUpdatedAt = DateTime.UtcNow.AddDays(-10);
        var beforeSet = DateTime.UtcNow.AddSeconds(-1);

        store.Set(new VRChatSessionToken
        {
            AccountId = "usr_123",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie"
            },
            UpdatedAtUtc = suppliedUpdatedAt
        });

        var afterSet = DateTime.UtcNow.AddSeconds(1);
        var restored = store.Get();

        Assert.True(File.Exists(path));
        Assert.NotNull(restored);
        Assert.NotEqual(suppliedUpdatedAt, restored.UpdatedAtUtc);
        Assert.InRange(restored.UpdatedAtUtc!.Value, beforeSet, afterSet);
    }

    [Fact]
    public void FileSessionStore_Get_ReturnsCaseInsensitiveMetadata()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "session.json");
        var store = new VRChatFileSessionStore(path);

        store.Set(new VRChatSessionToken
        {
            Metadata = new Dictionary<string, string>
            {
                ["AuthCookie"] = "auth-cookie"
            }
        });

        var restored = store.Get();

        Assert.NotNull(restored);
        Assert.Equal("auth-cookie", restored.Metadata!["authcookie"]);
    }

    [Fact]
    public void FileSessionStore_Set_ThrowsWhenTokenIsNull()
    {
        using var temp = new TempDirectory();
        var store = new VRChatFileSessionStore(Path.Combine(temp.Path, "session.json"));

        var exception = Assert.Throws<ArgumentNullException>(() => store.Set(null!));

        Assert.Equal("token", exception.ParamName);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_NoStoredSessionResetsPreviousState()
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
        var auth = new VRChatAuthService(store);

        var connected = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);
        store.Clear();
        var cleared = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(connected.IsConnected);
        Assert.False(cleared.HasStoredSession);
        Assert.False(cleared.IsConnected);
        Assert.False(cleared.RequiresTwoFactor);
        Assert.Null(cleared.UserId);
        Assert.Null(cleared.DisplayName);
        Assert.Null(cleared.AccountLogin);
        Assert.Null(cleared.LastVerifiedUtc);
        Assert.Null(cleared.UpdatedAtUtc);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_ExpiredStoredSessionClearsStoreAndReportsExpired()
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
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-31)
        });
        var auth = new VRChatAuthService(store);

        auth.Configure(new VRChatAuthOptions
        {
            AutoReconnect = true,
            CookieTtlDays = 30
        });

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.False(status.HasStoredSession);
        Assert.False(status.IsConnected);
        Assert.Equal("Stored VRChat session has expired.", status.LastError);
        Assert.Equal(1, store.ClearCount);
        Assert.Null(store.Peek());
    }

    [Fact]
    public async Task TryRestoreSessionAsync_UsesInjectedStoreWithoutRefresh()
    {
        var verifiedAt = DateTime.UtcNow.AddMinutes(-5);
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
                ["AccountLogin"] = "login@example.test",
                ["LastVerifiedUtc"] = verifiedAt.ToString("O", CultureInfo.InvariantCulture)
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var auth = new VRChatAuthService(store);

        auth.Configure(new VRChatAuthOptions
        {
            AutoReconnect = false,
            CookieTtlDays = 30
        });

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(status.HasStoredSession);
        Assert.True(status.IsConnected);
        Assert.False(status.RequiresTwoFactor);
        Assert.False(status.AutoReconnect);
        Assert.Equal("usr_123", status.UserId);
        Assert.Equal("Display", status.DisplayName);
        Assert.Equal("login@example.test", status.AccountLogin);
        Assert.NotNull(status.LastVerifiedUtc);
        Assert.Equal(1, store.GetCount);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_RestoresLowercaseMetadataKeys()
    {
        var verifiedAt = DateTime.UtcNow.AddMinutes(-10);
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            Metadata = new Dictionary<string, string>
            {
                ["authcookie"] = "auth-cookie",
                ["userid"] = "usr_123",
                ["displayname"] = "Display",
                ["accountlogin"] = "login@example.test",
                ["lastverifiedutc"] = verifiedAt.ToString("O", CultureInfo.InvariantCulture)
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var auth = new VRChatAuthService(store);

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(status.HasStoredSession);
        Assert.True(status.IsConnected);
        Assert.Equal("usr_123", status.UserId);
        Assert.Equal("Display", status.DisplayName);
        Assert.Equal("login@example.test", status.AccountLogin);
        Assert.NotNull(status.LastVerifiedUtc);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_InvalidLastVerifiedUtcLeavesTimestampNull()
    {
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            AccountId = "usr_123",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["LastVerifiedUtc"] = "not a timestamp"
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var auth = new VRChatAuthService(store);

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(status.IsConnected);
        Assert.Null(status.LastVerifiedUtc);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_RestoresEmailHintsAndUpdatedAtUtc()
    {
        var updatedAt = DateTime.UtcNow.AddMinutes(-3);
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            AccountId = "usr_123",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["EmailHint"] = "a***@example.test",
                ["PendingEmailHint"] = "p***@example.test"
            },
            UpdatedAtUtc = updatedAt
        });
        var auth = new VRChatAuthService(store);

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(status.IsConnected);
        Assert.Equal("a***@example.test", status.EmailHint);
        Assert.Equal("p***@example.test", status.PendingEmailHint);
        Assert.Equal(updatedAt, status.UpdatedAtUtc);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_RestoresPendingEmailCodeState()
    {
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            AccountLogin = "login@example.test",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["PendingTwoFactor"] = "true",
                ["RequiresEmail2FA"] = "true",
                ["TwoFactorMethods"] = "emailOtp",
                ["PendingEmailHint"] = "l***@example.test",
                ["AccountLogin"] = "login@example.test"
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var auth = new VRChatAuthService(store);

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.True(status.HasStoredSession);
        Assert.False(status.IsConnected);
        Assert.True(status.RequiresTwoFactor);
        Assert.True(status.RequiresEmailCode);
        Assert.Equal(new[] { "emailOtp" }, status.TwoFactorMethods);
        Assert.Equal("l***@example.test", status.PendingEmailHint);
        Assert.Equal("login@example.test", status.AccountLogin);
    }

    [Fact]
    public async Task TryRestoreSessionAsync_FiltersCompletedAuthenticatorAndKeepsEmailOtpPending()
    {
        var store = new FakeSessionStore(new VRChatSessionToken
        {
            AccountLogin = "login@example.test",
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AuthCookie"] = "auth-cookie",
                ["PendingTwoFactor"] = "true",
                ["TwoFactorMethods"] = "totp,emailOtp",
                ["CompletedTwoFactorMethods"] = "totp,otp",
                ["AccountLogin"] = "login@example.test"
            },
            UpdatedAtUtc = DateTime.UtcNow
        });
        var auth = new VRChatAuthService(store);

        var status = await auth.TryRestoreSessionAsync(forceRefresh: false, CancellationToken.None);

        Assert.False(status.IsConnected);
        Assert.True(status.RequiresTwoFactor);
        Assert.True(status.RequiresEmailCode);
        Assert.Equal(new[] { "emailOtp" }, status.TwoFactorMethods);
        Assert.Equal(new[] { "totp", "otp" }, status.CompletedTwoFactorMethods);
    }

    private sealed class FakeSessionStore(VRChatSessionToken? token) : IVRChatSessionStore
    {
        private VRChatSessionToken? _token = token?.Clone();

        public int GetCount { get; private set; }
        public int ClearCount { get; private set; }

        public VRChatSessionToken? Get()
        {
            GetCount++;
            return _token?.Clone();
        }

        public void Set(VRChatSessionToken token)
        {
            _token = token.Clone();
        }

        public void Clear()
        {
            ClearCount++;
            _token = null;
        }

        public VRChatSessionToken? Peek()
            => _token?.Clone();
    }
}
