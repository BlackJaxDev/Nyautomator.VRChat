namespace Nyautomator;

public sealed class VRChatModuleOptions
{
    public VRChatAuthOptions Auth { get; set; } = new();
    public VRChatDollyOptionsInput Dolly { get; set; } = new();
    public VRChatOscOptions Osc { get; set; } = new();

    public static VRChatModuleOptions CreateDefault(string? moduleDataDirectory = null)
    {
        var options = new VRChatModuleOptions();
        if (!string.IsNullOrWhiteSpace(moduleDataDirectory))
            options.Dolly.TrackDirectory = Path.Combine(moduleDataDirectory, "dolly", "tracks");

        return options;
    }
}

public sealed class VRChatOscOptions
{
    public bool Enabled { get; set; } = true;
    public int SenderPort { get; set; } = 9000;
    public int ListenerPort { get; set; } = 9001;
    public bool EnablePassthrough { get; set; }
    public int PassthroughInputPort { get; set; } = 9010;
    public int PassthroughOutputPort { get; set; } = 9011;
    public List<VRChatOscFilterRule> FilterRules { get; set; } = [];
    public VRChatOscFilterTypeSelection FilterTypes { get; set; } = new();
}

public sealed class VRChatOscFilterRule
{
    public string Action { get; set; } = "ignore";
    public string Match { get; set; } = "contains";
    public string? Value { get; set; } = string.Empty;
}

public sealed class VRChatOscFilterTypeSelection
{
    public bool Bool { get; set; } = true;
    public bool Int { get; set; } = true;
    public bool Float { get; set; } = true;
}
