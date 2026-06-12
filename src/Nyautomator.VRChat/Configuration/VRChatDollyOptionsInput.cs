namespace Nyautomator;

public sealed class VRChatDollyOptionsInput
{
    public bool Enabled { get; set; } = true;
    public string? TrackDirectory { get; set; }
    public int DefaultFrameRate { get; set; } = 60;
    public int PoseFreshnessMilliseconds { get; set; } = 2000;
    public int SettingsFreshnessMilliseconds { get; set; } = 5000;
    public bool EnableAvatarCaptureTrigger { get; set; }
    public string CaptureAvatarParameter { get; set; } = "NyautoDollyCapture";
    public bool EnableOpenVrCompanion { get; set; }
    public bool ConfirmWritesOnStartup { get; set; } = true;
}
