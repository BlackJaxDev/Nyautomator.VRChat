using System.Text.Json;

namespace Nyautomator;

public interface IVRChatSessionStore
{
    VRChatSessionToken? Get();
    void Set(VRChatSessionToken token);
    void Clear();
}

public sealed class VRChatSessionToken
{
    public string? AccountId { get; set; }
    public string? AccountLogin { get; set; }
    public string? AccountDisplayName { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public VRChatSessionToken Clone()
        => new()
        {
            AccountId = AccountId,
            AccountLogin = AccountLogin,
            AccountDisplayName = AccountDisplayName,
            Metadata = Metadata is null
                ? null
                : new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase),
            UpdatedAtUtc = UpdatedAtUtc
        };
}

public sealed class VRChatFileSessionStore : IVRChatSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;

    public VRChatFileSessionStore(string? path = null)
    {
        _path = string.IsNullOrWhiteSpace(path)
            ? Path.Combine(GetDefaultDataDirectory(), "session.json")
            : Path.GetFullPath(path);
    }

    public VRChatSessionToken? Get()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
                return null;

            try
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<VRChatSessionToken>(json, JsonOptions)?.Clone();
            }
            catch
            {
                return null;
            }
        }
    }

    public void Set(VRChatSessionToken token)
    {
        if (token is null)
            throw new ArgumentNullException(nameof(token));

        lock (_gate)
        {
            var clone = token.Clone();
            clone.UpdatedAtUtc = DateTime.UtcNow;
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_path, JsonSerializer.Serialize(clone, JsonOptions));
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                    File.Delete(_path);
            }
            catch
            {
            }
        }
    }

    private static string GetDefaultDataDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("NYAUTOMATOR_VRCHAT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(localAppData, "Nyautomator", "modules", "vrchat");

        return Path.Combine(AppContext.BaseDirectory, "settings", "modules", "vrchat");
    }
}
