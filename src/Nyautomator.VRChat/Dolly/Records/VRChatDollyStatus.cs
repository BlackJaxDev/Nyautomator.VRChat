using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Current status for VRChat dolly capture, playback, OSC health, and camera state.
/// </summary>
/// <param name="Enabled">Whether the dolly feature is enabled by configuration.</param>
/// <param name="OscSending">Whether OSC sending is active.</param>
/// <param name="OscListening">Whether OSC listening is active.</param>
/// <param name="PoseReadObserved">Whether any camera pose has been received.</param>
/// <param name="PoseWriteConfirmed">Whether a recent pose write appears to have been echoed back.</param>
/// <param name="SettingsReadObserved">Whether any camera settings have been received.</param>
/// <param name="SettingsWriteConfirmed">Whether a recent settings write appears to have been echoed back.</param>
/// <param name="LastPoseReceivedUtc">UTC time of the last received camera pose.</param>
/// <param name="LastSettingsReceivedUtc">UTC time of the last received camera settings update.</param>
/// <param name="LastPoseWriteUtc">UTC time of the last pose sent by the helper.</param>
/// <param name="LastSettingsWriteUtc">UTC time of the last settings command sent by the helper.</param>
/// <param name="ActiveCaptureTrackId">Track selected for avatar-triggered capture.</param>
/// <param name="TrackDirectory">Directory where track JSON files are stored.</param>
/// <param name="ActivePlaybacks">Currently active playback session snapshots.</param>
/// <param name="Camera">Latest cached VRChat user camera snapshot.</param>
public sealed record VRChatDollyStatus(
    bool Enabled,
    bool OscSending,
    bool OscListening,
    bool PoseReadObserved,
    bool PoseWriteConfirmed,
    bool SettingsReadObserved,
    bool SettingsWriteConfirmed,
    DateTime? LastPoseReceivedUtc,
    DateTime? LastSettingsReceivedUtc,
    DateTime? LastPoseWriteUtc,
    DateTime? LastSettingsWriteUtc,
    string? ActiveCaptureTrackId,
    string TrackDirectory,
    IReadOnlyList<VRChatDollyPlaybackState> ActivePlaybacks,
    VRChatCameraSnapshot Camera);
