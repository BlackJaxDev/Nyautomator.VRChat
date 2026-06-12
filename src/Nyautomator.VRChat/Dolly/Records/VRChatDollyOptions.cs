using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Normalized configuration values used by the VRChat dolly runtime.
/// </summary>
/// <param name="Enabled">Whether dolly capture and playback are enabled.</param>
/// <param name="TrackDirectory">Absolute directory where track files are stored.</param>
/// <param name="DefaultFrameRate">Default frame rate clamped to the supported playback range.</param>
/// <param name="PoseFreshnessMilliseconds">Maximum camera pose age considered fresh.</param>
/// <param name="SettingsFreshnessMilliseconds">Maximum camera settings age considered fresh.</param>
/// <param name="EnableAvatarCaptureTrigger">Whether avatar parameter changes can trigger capture.</param>
/// <param name="CaptureAvatarParameter">Avatar parameter name or address used as the capture trigger.</param>
/// <param name="EnableOpenVrCompanion">Whether the OpenVR companion integration is enabled by configuration.</param>
/// <param name="ConfirmWritesOnStartup">Whether startup should verify OSC writes through camera echo state.</param>
internal sealed record VRChatDollyOptions(
    bool Enabled,
    string TrackDirectory,
    int DefaultFrameRate,
    int PoseFreshnessMilliseconds,
    int SettingsFreshnessMilliseconds,
    bool EnableAvatarCaptureTrigger,
    string CaptureAvatarParameter,
    bool EnableOpenVrCompanion,
    bool ConfirmWritesOnStartup)
{
    /// <summary>
    /// Creates options from the default application dolly configuration.
    /// </summary>
    /// <returns>Normalized default options.</returns>
    public static VRChatDollyOptions Default(string? defaultTrackDirectory = null)
    {
        return FromConfiguration(null, defaultTrackDirectory);
    }

    /// <summary>
    /// Converts application configuration into normalized dolly runtime options.
    /// </summary>
    /// <param name="options">Module dolly options, or null for defaults.</param>
    /// <param name="defaultTrackDirectory">Default track directory supplied by the module host.</param>
    /// <returns>Normalized dolly runtime options.</returns>
    public static VRChatDollyOptions FromConfiguration(VRChatDollyOptionsInput? options, string? defaultTrackDirectory = null)
    {
        options ??= new VRChatDollyOptionsInput();
        var defaultDirectory = string.IsNullOrWhiteSpace(defaultTrackDirectory)
            ? GetStandaloneDefaultTrackDirectory()
            : Path.GetFullPath(defaultTrackDirectory);

        return new VRChatDollyOptions(
            options.Enabled,
            string.IsNullOrWhiteSpace(options.TrackDirectory) ? defaultDirectory : Path.GetFullPath(options.TrackDirectory),
            Math.Clamp(options.DefaultFrameRate <= 0 ? 60 : options.DefaultFrameRate, 1, 120),
            Math.Max(250, options.PoseFreshnessMilliseconds <= 0 ? 2000 : options.PoseFreshnessMilliseconds),
            Math.Max(250, options.SettingsFreshnessMilliseconds <= 0 ? 5000 : options.SettingsFreshnessMilliseconds),
            options.EnableAvatarCaptureTrigger,
            string.IsNullOrWhiteSpace(options.CaptureAvatarParameter) ? "NyautoDollyCapture" : options.CaptureAvatarParameter.Trim(),
            options.EnableOpenVrCompanion,
            options.ConfirmWritesOnStartup);
    }

    internal static string GetStandaloneDefaultTrackDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("NYAUTOMATOR_VRCHAT_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.Combine(Path.GetFullPath(configured), "dolly", "tracks");

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            return Path.Combine(localAppData, "Nyautomator", "modules", "vrchat", "dolly", "tracks");

        return Path.Combine(AppContext.BaseDirectory, "settings", "modules", "vrchat", "dolly", "tracks");
    }
}
