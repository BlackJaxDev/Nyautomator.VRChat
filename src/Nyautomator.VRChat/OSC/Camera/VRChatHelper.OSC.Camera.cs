using Newtonsoft.Json;
using OscCore;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Nyautomator
{
    /// <summary>
    /// Shared VRChat helper namespace for user camera OSC controls.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper for VRChat user camera state, controls, and conversions.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Known VRChat user camera mode definitions keyed by display name.
            /// </summary>
            private static readonly (string Name, OscCameraModeDefinition Definition)[] CameraModeDefinitions =
            [
                ("Off", new OscCameraModeDefinition(0, "Disable the camera.")),
                ("Photo", new OscCameraModeDefinition(1, "Photo mode.")),
                ("Stream", new OscCameraModeDefinition(2, "Stream mode.")),
                ("Emoji", new OscCameraModeDefinition(3, "Emoji mode.")),
                ("Multilayer", new OscCameraModeDefinition(4, "Multilayer mode.")),
                ("Print", new OscCameraModeDefinition(5, "Polaroid/Print mode.")),
                ("Drone", new OscCameraModeDefinition(6, "Drone mode.")),
            ];

            /// <summary>
            /// Lookup table for camera modes using normalized mode names.
            /// </summary>
            private static readonly Dictionary<string, OscCameraModeDefinition> CameraModesLookup =
                CameraModeDefinitions.ToDictionary(static entry => NormalizeCameraKey(entry.Name), static entry => entry.Definition, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Known VRChat user camera boolean controls.
            /// </summary>
            private static readonly (string Name, OscCameraToggleDefinition Definition)[] CameraToggleDefinitions =
            [
                ("ShowUIInCamera", new OscCameraToggleDefinition("/usercamera/ShowUIInCamera", "Show camera UI mask.")),
                ("Lock", new OscCameraToggleDefinition("/usercamera/Lock", "Toggle lock.")),
                ("LocalPlayer", new OscCameraToggleDefinition("/usercamera/LocalPlayer", "Toggle local player mask.")),
                ("RemotePlayer", new OscCameraToggleDefinition("/usercamera/RemotePlayer", "Toggle remote players mask.")),
                ("Environment", new OscCameraToggleDefinition("/usercamera/Environment", "Toggle environment mask.")),
                ("GreenScreen", new OscCameraToggleDefinition("/usercamera/GreenScreen", "Toggle greenscreen.")),
                ("SmoothMovement", new OscCameraToggleDefinition("/usercamera/SmoothMovement", "Toggle smoothing.")),
                ("LookAtMe", new OscCameraToggleDefinition("/usercamera/LookAtMe", "Toggle look-at-me.")),
                ("AutoLevelRoll", new OscCameraToggleDefinition("/usercamera/AutoLevelRoll", "Toggle auto-level roll.")),
                ("AutoLevelPitch", new OscCameraToggleDefinition("/usercamera/AutoLevelPitch", "Toggle auto-level pitch.")),
                ("Flying", new OscCameraToggleDefinition("/usercamera/Flying", "Toggle flying.")),
                ("TriggerTakesPhotos", new OscCameraToggleDefinition("/usercamera/TriggerTakesPhotos", "Toggle trigger capture.")),
                ("DollyPathsStayVisible", new OscCameraToggleDefinition("/usercamera/DollyPathsStayVisible", "Toggle dolly paths visibility.")),
                ("CameraEars", new OscCameraToggleDefinition("/usercamera/CameraEars", "Toggle audio capture.")),
                ("ShowFocus", new OscCameraToggleDefinition("/usercamera/ShowFocus", "Toggle focus overlay.")),
                ("Streaming", new OscCameraToggleDefinition("/usercamera/Streaming", "Toggle spout stream.")),
                ("RollWhileFlying", new OscCameraToggleDefinition("/usercamera/RollWhileFlying", "Toggle roll while flying.")),
                ("OrientationIsLandscape", new OscCameraToggleDefinition("/usercamera/OrientationIsLandscape", "Toggle orientation.")),
            ];

            /// <summary>
            /// Lookup table for camera toggles using normalized toggle names.
            /// </summary>
            private static readonly Dictionary<string, OscCameraToggleDefinition> CameraToggleLookup =
                CameraToggleDefinitions.ToDictionary(static entry => NormalizeCameraKey(entry.Name), static entry => entry.Definition, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Known VRChat user camera numeric slider controls and their ranges.
            /// </summary>
            private static readonly (string Name, OscCameraSliderDefinition Definition)[] CameraSliderDefinitions =
            [
                ("Zoom", new OscCameraSliderDefinition("/usercamera/Zoom", 20f, 150f, 45f, "Zoom.")),
                ("Exposure", new OscCameraSliderDefinition("/usercamera/Exposure", -10f, 4f, 0f, "Exposure.")),
                ("FocalDistance", new OscCameraSliderDefinition("/usercamera/FocalDistance", 0f, 10f, 1.5f, "Focal distance.")),
                ("Aperture", new OscCameraSliderDefinition("/usercamera/Aperture", 1.4f, 32f, 15f, "Aperture.")),
                ("Hue", new OscCameraSliderDefinition("/usercamera/Hue", 0f, 360f, 120f, "Greenscreen hue.")),
                ("Saturation", new OscCameraSliderDefinition("/usercamera/Saturation", 0f, 100f, 100f, "Greenscreen saturation.")),
                ("Lightness", new OscCameraSliderDefinition("/usercamera/Lightness", 0f, 50f, 60f, "Greenscreen lightness.")),
                ("LookAtMeXOffset", new OscCameraSliderDefinition("/usercamera/LookAtMeXOffset", -25f, 25f, 0f, "Look-at-me X offset.")),
                ("LookAtMeYOffset", new OscCameraSliderDefinition("/usercamera/LookAtMeYOffset", -25f, 25f, 0f, "Look-at-me Y offset.")),
                ("FlySpeed", new OscCameraSliderDefinition("/usercamera/FlySpeed", 0.1f, 15f, 3f, "Fly speed.")),
                ("TurnSpeed", new OscCameraSliderDefinition("/usercamera/TurnSpeed", 0.1f, 5f, 1f, "Turn speed.")),
                ("SmoothingStrength", new OscCameraSliderDefinition("/usercamera/SmoothingStrength", 0.1f, 10f, 5f, "Smoothing strength.")),
                ("PhotoRate", new OscCameraSliderDefinition("/usercamera/PhotoRate", 0.1f, 2f, 1f, "Dolly photo capture rate.")),
                ("Duration", new OscCameraSliderDefinition("/usercamera/Duration", 0.1f, 60f, 2f, "Dolly duration.")),
            ];

            /// <summary>
            /// Lookup table for camera sliders using normalized slider names.
            /// </summary>
            private static readonly Dictionary<string, OscCameraSliderDefinition> CameraSliderLookup =
                CameraSliderDefinitions.ToDictionary(static entry => NormalizeCameraKey(entry.Name), static entry => entry.Definition, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Known VRChat user camera action controls.
            /// </summary>
            private static readonly (string Name, OscCameraActionDefinition Definition)[] CameraActionDefinitions =
            [
                ("Close", new OscCameraActionDefinition("/usercamera/Close", "Close the camera.")),
                ("Capture", new OscCameraActionDefinition("/usercamera/Capture", "Take a photo.")),
                ("CaptureDelayed", new OscCameraActionDefinition("/usercamera/CaptureDelayed", "Take a timed photo.")),
            ];

            /// <summary>
            /// Lookup table for camera actions using normalized action names.
            /// </summary>
            private static readonly Dictionary<string, OscCameraActionDefinition> CameraActionLookup =
                CameraActionDefinitions.ToDictionary(static entry => NormalizeCameraKey(entry.Name), static entry => entry.Definition, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// OSC path prefix used by VRChat user camera messages.
            /// </summary>
            private const string UserCameraPathPrefix = "/usercamera/";

            /// <summary>
            /// OSC address used by VRChat user camera mode messages.
            /// </summary>
            private const string CameraModeAddress = "/usercamera/Mode";

            /// <summary>
            /// OSC address used by VRChat user camera pose messages.
            /// </summary>
            private const string CameraPoseAddress = "/usercamera/Pose";

            /// <summary>
            /// Maps numeric camera mode values back to known mode names.
            /// </summary>
            private static readonly Dictionary<int, string> CameraModeNameByValue =
                CameraModeDefinitions.ToDictionary(static entry => entry.Definition.Mode, static entry => entry.Name);

            /// <summary>
            /// Maps camera toggle OSC addresses to logical toggle names.
            /// </summary>
            private static readonly Dictionary<string, string> CameraToggleNameByAddress =
                CameraToggleDefinitions.ToDictionary(static entry => entry.Definition.Address, static entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Maps camera slider OSC addresses to logical slider names.
            /// </summary>
            private static readonly Dictionary<string, string> CameraSliderNameByAddress =
                CameraSliderDefinitions.ToDictionary(static entry => entry.Definition.Address, static entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Maps camera action OSC addresses to logical action names.
            /// </summary>
            private static readonly Dictionary<string, string> CameraActionNameByAddress =
                CameraActionDefinitions.ToDictionary(static entry => entry.Definition.Address, static entry => entry.Name, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Synchronizes access to cached camera state.
            /// </summary>
            private static readonly object CameraStateSync = new();

            /// <summary>
            /// Latest known numeric camera mode received from VRChat.
            /// </summary>
            private static int? CurrentCameraMode;

            /// <summary>
            /// Latest known camera mode name received from VRChat.
            /// </summary>
            private static string? CurrentCameraModeName;

            /// <summary>
            /// Latest known camera pose received from VRChat.
            /// </summary>
            private static VRChatCameraPoseSnapshot? CurrentCameraPose;

            /// <summary>
            /// Latest known camera toggle values keyed by logical toggle name.
            /// </summary>
            private static readonly Dictionary<string, bool> CurrentCameraToggles = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Latest known camera slider values keyed by logical slider name.
            /// </summary>
            private static readonly Dictionary<string, float> CurrentCameraSliders = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// UTC time when camera pose data was last received.
            /// </summary>
            private static DateTime? LastCameraPoseReceivedUtc;

            /// <summary>
            /// UTC time when camera settings data was last received.
            /// </summary>
            private static DateTime? LastCameraSettingsReceivedUtc;

            /// <summary>
            /// UTC time when camera pose data was last written by this helper.
            /// </summary>
            private static DateTime? LastCameraPoseWriteUtc;

            /// <summary>
            /// UTC time when camera settings data was last written by this helper.
            /// </summary>
            private static DateTime? LastCameraSettingsWriteUtc;

            /// <summary>
            /// Gets or sets how recently pose data must have been observed to be considered fresh.
            /// </summary>
            public static TimeSpan CameraPoseFreshness { get; set; } = TimeSpan.FromSeconds(2);

            /// <summary>
            /// Gets or sets how recently camera settings data must have been observed to be considered fresh.
            /// </summary>
            public static TimeSpan CameraSettingsFreshness { get; set; } = TimeSpan.FromSeconds(5);

            /// <summary>
            /// Gets known user camera mode names.
            /// </summary>
            public static IEnumerable<string> CameraModeNames => CameraModeDefinitions.Select(static entry => entry.Name);

            /// <summary>
            /// Gets known user camera toggle names.
            /// </summary>
            public static IEnumerable<string> CameraToggleNames => CameraToggleDefinitions.Select(static entry => entry.Name);

            /// <summary>
            /// Gets known user camera slider names.
            /// </summary>
            public static IEnumerable<string> CameraSliderNames => CameraSliderDefinitions.Select(static entry => entry.Name);

            /// <summary>
            /// Gets known user camera action names.
            /// </summary>
            public static IEnumerable<string> CameraActionNames => CameraActionDefinitions.Select(static entry => entry.Name);

            /// <summary>
            /// Gets a thread-safe snapshot of the latest cached user camera state.
            /// </summary>
            public static VRChatCameraSnapshot CurrentCameraSnapshot
            {
                get
                {
                    lock (CameraStateSync)
                    {
                        var now = DateTime.UtcNow;
                        var poseFresh = LastCameraPoseReceivedUtc.HasValue
                            && now - LastCameraPoseReceivedUtc.Value <= CameraPoseFreshness;
                        var settingsFresh = LastCameraSettingsReceivedUtc.HasValue
                            && now - LastCameraSettingsReceivedUtc.Value <= CameraSettingsFreshness;

                        return new VRChatCameraSnapshot(
                            now,
                            CurrentCameraMode,
                            CurrentCameraModeName,
                            CurrentCameraPose,
                            new Dictionary<string, bool>(CurrentCameraToggles, StringComparer.OrdinalIgnoreCase),
                            new Dictionary<string, float>(CurrentCameraSliders, StringComparer.OrdinalIgnoreCase),
                            poseFresh,
                            settingsFresh,
                            LastCameraPoseReceivedUtc,
                            LastCameraSettingsReceivedUtc);
                    }
                }
            }

            /// <summary>
            /// Gets when this helper last wrote a camera pose command.
            /// </summary>
            public static DateTime? LastUserCameraPoseWriteUtc
            {
                get
                {
                    lock (CameraStateSync)
                    {
                        return LastCameraPoseWriteUtc;
                    }
                }
            }

            /// <summary>
            /// Gets when this helper last wrote a camera setting command.
            /// </summary>
            public static DateTime? LastUserCameraSettingsWriteUtc
            {
                get
                {
                    lock (CameraStateSync)
                    {
                        return LastCameraSettingsWriteUtc;
                    }
                }
            }

            /// <summary>
            /// Resolves a known camera mode by name.
            /// </summary>
            /// <param name="modeName">Camera mode name or address-style key.</param>
            /// <param name="mode">Resolved camera mode definition.</param>
            /// <returns><see langword="true"/> when the mode exists.</returns>
            public static bool TryGetCameraMode(string? modeName, out OscCameraModeDefinition mode)
            {
                mode = default;
                if (string.IsNullOrWhiteSpace(modeName))
                    return false;

                return CameraModesLookup.TryGetValue(NormalizeCameraKey(modeName), out mode);
            }

            /// <summary>
            /// Resolves a known camera toggle by name.
            /// </summary>
            /// <param name="toggleName">Camera toggle name or address-style key.</param>
            /// <param name="toggle">Resolved camera toggle definition.</param>
            /// <returns><see langword="true"/> when the toggle exists.</returns>
            public static bool TryGetCameraToggle(string? toggleName, out OscCameraToggleDefinition toggle)
            {
                toggle = default;
                if (string.IsNullOrWhiteSpace(toggleName))
                    return false;

                return CameraToggleLookup.TryGetValue(NormalizeCameraKey(toggleName), out toggle);
            }

            /// <summary>
            /// Resolves a known camera slider by name.
            /// </summary>
            /// <param name="sliderName">Camera slider name or address-style key.</param>
            /// <param name="slider">Resolved camera slider definition.</param>
            /// <returns><see langword="true"/> when the slider exists.</returns>
            public static bool TryGetCameraSlider(string? sliderName, out OscCameraSliderDefinition slider)
            {
                slider = default;
                if (string.IsNullOrWhiteSpace(sliderName))
                    return false;

                return CameraSliderLookup.TryGetValue(NormalizeCameraKey(sliderName), out slider);
            }

            /// <summary>
            /// Resolves a known camera action by name.
            /// </summary>
            /// <param name="actionName">Camera action name or address-style key.</param>
            /// <param name="action">Resolved camera action definition.</param>
            /// <returns><see langword="true"/> when the action exists.</returns>
            public static bool TryGetCameraAction(string? actionName, out OscCameraActionDefinition action)
            {
                action = default;
                if (string.IsNullOrWhiteSpace(actionName))
                    return false;

                return CameraActionLookup.TryGetValue(NormalizeCameraKey(actionName), out action);
            }

            /// <summary>
            /// Sends a clamped numeric camera mode value to VRChat over OSC.
            /// </summary>
            /// <param name="mode">Numeric camera mode value.</param>
            public static void SetCameraMode(int mode)
            {
                if (!Sending)
                    return;

                var clamped = Math.Clamp(mode, 0, 6);
                SendOscMessage(new OscMessage("/usercamera/Mode", clamped));
                NoteCameraSettingsWrite();
            }

            /// <summary>
            /// Sends a camera mode to VRChat by name or numeric string.
            /// </summary>
            /// <param name="modeName">Camera mode name or numeric mode text.</param>
            public static void SetCameraMode(string? modeName)
            {
                if (!Sending || string.IsNullOrWhiteSpace(modeName))
                    return;

                if (TryGetCameraMode(modeName, out var definition))
                {
                    SetCameraMode(definition.Mode);
                    return;
                }

                if (int.TryParse(modeName, out var numericMode))
                    SetCameraMode(numericMode);
            }

            /// <summary>
            /// Sends a camera toggle value to VRChat by known name or address suffix.
            /// </summary>
            /// <param name="toggleName">Camera toggle name or address-style key.</param>
            /// <param name="value">Toggle value to send.</param>
            public static void SetCameraToggle(string? toggleName, bool value)
            {
                if (!Sending || string.IsNullOrWhiteSpace(toggleName))
                    return;

                var key = NormalizeCameraKey(toggleName);
                var address = $"/usercamera/{key}";

                if (CameraToggleLookup.TryGetValue(key, out var toggle))
                    address = toggle.Address;

                SendOscMessage(new OscMessage(address, value));
                NoteCameraSettingsWrite();
            }

            /// <summary>
            /// Sends a camera slider value to VRChat, clamping known sliders to their supported range.
            /// </summary>
            /// <param name="sliderName">Camera slider name or address-style key.</param>
            /// <param name="value">Slider value to send.</param>
            public static void SetCameraSlider(string? sliderName, float value)
            {
                if (!Sending || string.IsNullOrWhiteSpace(sliderName))
                    return;

                var key = NormalizeCameraKey(sliderName);
                var address = $"/usercamera/{key}";
                var payload = value;

                if (CameraSliderLookup.TryGetValue(key, out var slider))
                {
                    address = slider.Address;
                    payload = Math.Clamp(value, slider.Min, slider.Max);
                }

                SendOscMessage(new OscMessage(address, payload));
                NoteCameraSettingsWrite();
            }

            /// <summary>
            /// Sends a camera action trigger to VRChat by known name or address suffix.
            /// </summary>
            /// <param name="actionName">Camera action name or address-style key.</param>
            /// <param name="payload">Numeric payload sent with the action.</param>
            public static void TriggerCameraAction(string? actionName, float payload = 1f)
            {
                if (!Sending || string.IsNullOrWhiteSpace(actionName))
                    return;

                var key = NormalizeCameraKey(actionName);
                var address = $"/usercamera/{key}";

                if (CameraActionLookup.TryGetValue(key, out var action))
                    address = action.Address;

                SendOscMessage(new OscMessage(address, payload));
            }

            /// <summary>
            /// Sends a camera position and Euler rotation to VRChat over OSC.
            /// </summary>
            /// <param name="position">Camera position vector.</param>
            /// <param name="eulerDegrees">Camera rotation expressed as Euler degrees.</param>
            public static void SetCameraPose(Vector3 position, Vector3 eulerDegrees)
            {
                if (!Sending)
                    return;

                SendOscMessage(new OscMessage("/usercamera/Pose",
                    position.X,
                    position.Y,
                    position.Z,
                    eulerDegrees.X,
                    eulerDegrees.Y,
                    eulerDegrees.Z));
                NoteCameraPoseWrite();
            }

            /// <summary>
            /// Converts a quaternion to Euler degrees and sends the camera pose to VRChat.
            /// </summary>
            /// <param name="position">Camera position vector.</param>
            /// <param name="rotation">Camera rotation quaternion.</param>
            public static void SetCameraPose(Vector3 position, Quaternion rotation)
            {
                var eulerDegrees = QuaternionToEulerDegrees(rotation);
                SetCameraPose(position, eulerDegrees);
            }

            /// <summary>
            /// Normalizes camera keys by removing the usercamera prefix and spaces.
            /// </summary>
            /// <param name="name">Raw camera key, name, or OSC address.</param>
            /// <returns>Normalized lookup key.</returns>
            private static string NormalizeCameraKey(string name)
            {
                var key = name.Trim();
                if (key.StartsWith("/usercamera/", StringComparison.OrdinalIgnoreCase))
                    key = key[12..];

                return key.Replace(" ", string.Empty);
            }

            /// <summary>
            /// Metadata for a VRChat camera mode.
            /// </summary>
            /// <param name="Mode">Numeric VRChat camera mode value.</param>
            /// <param name="Description">Human-readable mode description.</param>
            public readonly record struct OscCameraModeDefinition(int Mode, string Description);

            /// <summary>
            /// Metadata for a VRChat camera toggle.
            /// </summary>
            /// <param name="Address">OSC address for the toggle.</param>
            /// <param name="Description">Human-readable toggle description.</param>
            public readonly record struct OscCameraToggleDefinition(string Address, string Description);

            /// <summary>
            /// Metadata for a VRChat camera slider.
            /// </summary>
            /// <param name="Address">OSC address for the slider.</param>
            /// <param name="Min">Minimum supported value.</param>
            /// <param name="Max">Maximum supported value.</param>
            /// <param name="Default">Default value used by callers when no value is supplied.</param>
            /// <param name="Description">Human-readable slider description.</param>
            public readonly record struct OscCameraSliderDefinition(string Address, float Min, float Max, float Default, string Description);

            /// <summary>
            /// Metadata for a VRChat camera action.
            /// </summary>
            /// <param name="Address">OSC address for the action.</param>
            /// <param name="Description">Human-readable action description.</param>
            public readonly record struct OscCameraActionDefinition(string Address, string Description);

            /// <summary>
            /// Multiplier used to convert degrees to radians.
            /// </summary>
            private const float DegreeToRadian = MathF.PI / 180f;

            /// <summary>
            /// Multiplier used to convert radians to degrees.
            /// </summary>
            private const float RadianToDegree = 180f / MathF.PI;

            /// <summary>
            /// Minimum quaternion length used when deciding whether to normalize or replace with identity.
            /// </summary>
            private const float QuaternionEpsilon = 1e-6f;

            /// <summary>
            /// Converts Euler degrees to a normalized quaternion using yaw, pitch, and roll.
            /// </summary>
            /// <param name="degrees">Euler angles in degrees.</param>
            /// <returns>Normalized quaternion rotation.</returns>
            internal static Quaternion EulerDegreesToQuaternion(Vector3 degrees)
            {
                var radians = degrees * DegreeToRadian;
                return SafeNormalize(Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z));
            }

            /// <summary>
            /// Converts a quaternion to Euler degrees using the helper's camera rotation convention.
            /// </summary>
            /// <param name="quaternion">Quaternion to convert.</param>
            /// <returns>Euler angles in degrees.</returns>
            internal static Vector3 QuaternionToEulerDegrees(Quaternion quaternion)
            {
                quaternion = SafeNormalize(quaternion);
                var matrix = Matrix4x4.CreateFromQuaternion(quaternion);

                var pitch = MathF.Asin(Math.Clamp(-matrix.M32, -1f, 1f));
                float yaw;
                float roll;

                if (MathF.Abs(MathF.Cos(pitch)) < QuaternionEpsilon)
                {
                    yaw = MathF.Atan2(-matrix.M13, matrix.M11);
                    roll = 0f;
                }
                else
                {
                    yaw = MathF.Atan2(matrix.M31, matrix.M33);
                    roll = MathF.Atan2(matrix.M12, matrix.M22);
                }

                return new Vector3(pitch * RadianToDegree, yaw * RadianToDegree, roll * RadianToDegree);
            }

            /// <summary>
            /// Normalizes a quaternion or returns identity when the quaternion is too close to zero length.
            /// </summary>
            /// <param name="quaternion">Quaternion to normalize.</param>
            /// <returns>A safe normalized quaternion.</returns>
            internal static Quaternion SafeNormalize(Quaternion quaternion)
            {
                return quaternion.LengthSquared() < QuaternionEpsilon
                    ? Quaternion.Identity
                    : Quaternion.Normalize(quaternion);
            }

            /// <summary>
            /// Updates cached camera mode state from an incoming OSC message.
            /// </summary>
            /// <param name="mode">Numeric camera mode value.</param>
            /// <param name="modeName">Resolved mode name.</param>
            private static void UpdateCameraMode(int mode, string modeName)
            {
                lock (CameraStateSync)
                {
                    CurrentCameraMode = mode;
                    CurrentCameraModeName = modeName;
                    LastCameraSettingsReceivedUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Updates cached camera pose state from an incoming OSC message.
            /// </summary>
            /// <param name="pose">Pose snapshot to cache.</param>
            private static void UpdateCameraPose(VRChatCameraPoseSnapshot pose)
            {
                lock (CameraStateSync)
                {
                    CurrentCameraPose = pose;
                    LastCameraPoseReceivedUtc = pose.UpdatedAtUtc;
                }
            }

            /// <summary>
            /// Updates cached camera toggle state from an incoming OSC message.
            /// </summary>
            /// <param name="toggleName">Logical toggle name.</param>
            /// <param name="enabled">Toggle value to cache.</param>
            private static void UpdateCameraToggle(string toggleName, bool enabled)
            {
                lock (CameraStateSync)
                {
                    CurrentCameraToggles[toggleName] = enabled;
                    LastCameraSettingsReceivedUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Updates cached camera slider state from an incoming OSC message.
            /// </summary>
            /// <param name="sliderName">Logical slider name.</param>
            /// <param name="value">Slider value to cache.</param>
            private static void UpdateCameraSlider(string sliderName, float value)
            {
                lock (CameraStateSync)
                {
                    CurrentCameraSliders[sliderName] = value;
                    LastCameraSettingsReceivedUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Records that this helper wrote a camera pose command.
            /// </summary>
            private static void NoteCameraPoseWrite()
            {
                lock (CameraStateSync)
                {
                    LastCameraPoseWriteUtc = DateTime.UtcNow;
                }
            }

            /// <summary>
            /// Records that this helper wrote a camera settings command.
            /// </summary>
            private static void NoteCameraSettingsWrite()
            {
                lock (CameraStateSync)
                {
                    LastCameraSettingsWriteUtc = DateTime.UtcNow;
                }
            }
        }
    }
}
