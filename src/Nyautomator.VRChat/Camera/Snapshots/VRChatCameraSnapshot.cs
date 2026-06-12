namespace Nyautomator;

/// <summary>
/// Immutable snapshot of the current cached VRChat user camera state.
/// </summary>
/// <param name="CapturedAtUtc">UTC time when the snapshot was built.</param>
/// <param name="Mode">Latest known numeric camera mode.</param>
/// <param name="ModeName">Latest known camera mode name.</param>
/// <param name="Pose">Latest known camera pose snapshot.</param>
/// <param name="Toggles">Latest known camera toggle values.</param>
/// <param name="Sliders">Latest known camera slider values.</param>
/// <param name="PoseFresh">Whether pose data is within the configured freshness window.</param>
/// <param name="SettingsFresh">Whether mode, slider, or toggle data is within the configured freshness window.</param>
/// <param name="LastPoseReceivedUtc">UTC time when pose data was last received.</param>
/// <param name="LastSettingsReceivedUtc">UTC time when camera settings data was last received.</param>
public sealed record VRChatCameraSnapshot(
    DateTime CapturedAtUtc,
    int? Mode,
    string? ModeName,
    VRChatCameraPoseSnapshot? Pose,
    IReadOnlyDictionary<string, bool> Toggles,
    IReadOnlyDictionary<string, float> Sliders,
    bool PoseFresh,
    bool SettingsFresh,
    DateTime? LastPoseReceivedUtc,
    DateTime? LastSettingsReceivedUtc);
