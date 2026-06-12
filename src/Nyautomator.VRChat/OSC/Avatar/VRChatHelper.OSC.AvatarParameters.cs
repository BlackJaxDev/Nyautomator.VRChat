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
    /// Shared VRChat helper namespace for avatar OSC parameter state.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that stores and sends typed avatar parameter values.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Latest integer avatar parameter values keyed by parameter name.
            /// </summary>
            private static Dictionary<string, int> IntParams { get; } = [];

            /// <summary>
            /// Latest boolean avatar parameter values keyed by parameter name.
            /// </summary>
            private static Dictionary<string, bool> BoolParams { get; } = [];

            /// <summary>
            /// Latest floating-point avatar parameter values keyed by parameter name.
            /// </summary>
            private static Dictionary<string, float> FloatParams { get; } = [];

            /// <summary>
            /// OSC address prefix used by VRChat avatar parameter messages.
            /// </summary>
            private const string AvatarParameterPathPrefix = "/avatar/parameters/";

            /// <summary>
            /// OSC address used when VRChat reports that the local avatar changed.
            /// </summary>
            private const string AvatarChangePath = "/avatar/change";

            /// <summary>
            /// Parameter type label used by VRChat for integer values.
            /// </summary>
            private const string Int = "Int";

            /// <summary>
            /// Parameter type label used by VRChat for boolean values.
            /// </summary>
            private const string Bool = "Bool";

            /// <summary>
            /// Parameter type label used by VRChat for floating-point values.
            /// </summary>
            private const string Float = "Float";

            /// <summary>
            /// Built-in VRChat avatar parameters that can arrive without an avatar config entry.
            /// </summary>
            private static readonly Dictionary<string, (string Type, string Description)> BuiltInParameterNames = new()
            {
                { "IsLocal", (Bool, "True if the avatar is being worn locally, false otherwise") },
                { "Viseme", (Int, "Oculus viseme index (0-14). When using Jawbone/Jawflap, range is 0-100 indicating volume") },
                { "Voice", (Float, "Microphone volume (0.0-1.0)") },
                { "GestureLeft", (Int, "Gesture from L hand control (0-7)") },
                { "GestureRight", (Int, "Gesture from R hand control (0-7)") },
                { "GestureLeftWeight", (Float, "Analog trigger L (0.0-1.0)�") },
                { "GestureRightWeight", (Float, "Analog trigger R (0.0-1.0)�") },
                { "AngularY", (Float, "Angular velocity on the Y axis") },
                { "VelocityX", (Float, "Lateral move speed in m/s") },
                { "VelocityY", (Float, "Vertical move speed in m/s") },
                { "VelocityZ", (Float, "Forward move speed in m/s") },
                { "VelocityMagnitude", (Float, "Total magnitude of velocity") },
                { "Upright", (Float, "How \"upright\" you are. 0 is prone, 1 is standing straight up") },
                { "Grounded", (Bool, "True if player touching ground") },
                { "Seated", (Bool, "True if player in station") },
                { "AFK", (Bool, "Is player unavailable (HMD proximity sensor / End key)") },
                { "TrackingType", (Int, "See description below") },
                { "VRMode", (Int, "Returns 1 if the user is in VR, 0 if they are not") },
                { "MuteSelf", (Bool, "Returns true if the user has muted themselves, false if unmuted") },
                { "InStation", (Bool, "Returns true if the user is in a station, false if not") },
                { "Earmuffs", (Bool, "Returns true if the user's Earmuff feature is on, false if not") },
                { "IsOnFriendsList", (Bool, "Returns true if the user viewing the avatar is friends with the user wearing it. false locally.") },
                { "AvatarVersion", (Int, "Returns 3 if the avatar was built using VRChat's SDK3 (2020.3.2) or later, 0 if not") },
                { "ScaleModified", (Bool, "Returns true if the user is scaled using avatar scaling, false if the avatar is at its default size") },
                { "ScaleFactor", (Float, "Relation between the avatar's default height and the current height. An avatar with a default eye-height of 1m scaled to 2m will report 2") },
                { "ScaleFactorInverse", (Float, "Inverse relation (1/x) between the avatar's default height and the current height. An avatar with a default eye-height of 1m scaled to 2m will report 0.5. Might be inaccurate at extremes") },
                { "EyeHeightAsMeters", (Float, "The avatar's eye height in meters") },
                { "EyeHeightAsPercent", (Float, "Relation of the avatar's eye height in meters relative to the default scaling limits (0.2-5.0). An avatar scaled to 2m will report (2.0 - 0.2) / (5.0 - 0.2) = 0.375") }
            };

            /// <summary>
            /// Routes an incoming avatar parameter OSC message through built-in or configured parameter handling.
            /// </summary>
            /// <param name="message">OSC message whose address starts with /avatar/parameters/.</param>
            private static void HandleAvatarParameters(OscMessage message)
            {
                string parameterName = message.Address[AvatarParameterPathPrefix.Length..];
                var handledBuiltIn = BuiltInParameterNames.ContainsKey(parameterName);
                if (handledBuiltIn)
                {
                    ProcessInput(null, message);
                }

                if (Config?.Parameters is null)
                {
                    if (!handledBuiltIn)
                        PublishAvatarParameterChanged(parameterName, "Unknown", message);
                    return;
                }

                var matched = false;
                foreach (var parameter in Config.Parameters)
                {
                    if (!(parameter?.Name?.Equals(parameterName) ?? false))
                        continue;

                    matched = true;
                    if (parameter.Input != null && (parameter.Input?.Address?.Equals(message.Address) ?? false))
                        ProcessInput(parameter, message);

                    //Technically outputs are handled by the VRChat client
                    //if (parameter.Output != null)
                    //    SendOutput(parameter);
                }

                if (!matched && !handledBuiltIn)
                    PublishAvatarParameterChanged(parameterName, "Unknown", message);
            }

            /// <summary>
            /// Stores an incoming avatar parameter value in the matching typed cache and publishes a change event.
            /// </summary>
            /// <param name="parameter">Configured avatar parameter, or <see langword="null"/> for a built-in parameter.</param>
            /// <param name="message">OSC message that contains the parameter value.</param>
            private static void ProcessInput(AvatarParameter? parameter, OscMessage message)
            {
                if (parameter is null)
                {
                    string parameterName = message.Address[AvatarParameterPathPrefix.Length..];
                    if (BuiltInParameterNames.TryGetValue(parameterName, out var builtin))
                    {
                        switch (builtin.Type)
                        {
                            case Int:
                                int intValueBI = (int)message[0];
                                if (!IntParams.TryAdd(parameterName, intValueBI))
                                    IntParams[parameterName] = intValueBI;
                                PublishAvatarParameterChanged(parameterName, builtin.Type, message);
                                break;
                            case Bool:
                                bool boolValueBI = (bool)message[0];
                                if (!BoolParams.TryAdd(parameterName, boolValueBI))
                                    BoolParams[parameterName] = boolValueBI;
                                PublishAvatarParameterChanged(parameterName, builtin.Type, message);
                                break;
                            case Float:
                                float floatValueBI = (float)message[0];
                                if (!FloatParams.TryAdd(parameterName, floatValueBI))
                                    FloatParams[parameterName] = floatValueBI;
                                PublishAvatarParameterChanged(parameterName, builtin.Type, message);
                                break;
                        }
                    }
                    return;
                }

                if (parameter.Input is null || parameter.Name is null)
                    return;

                switch (parameter.Input.Type)
                {
                    case Int:
                        int intValue = (int)message[0];
                        if (!IntParams.TryAdd(parameter.Name, intValue))
                            IntParams[parameter.Name] = intValue;
                        PublishAvatarParameterChanged(parameter.Name, parameter.Input.Type, message);
                        //Console.WriteLine($"Parameter '{parameter.Name}' set to (Int): {intValue}");
                        break;
                    case Bool:
                        bool boolValue = (bool)message[0];
                        if (!BoolParams.TryAdd(parameter.Name, boolValue))
                            BoolParams[parameter.Name] = boolValue;
                        PublishAvatarParameterChanged(parameter.Name, parameter.Input.Type, message);
                        //Console.WriteLine($"Parameter '{parameter.Name}' set to (Bool): {boolValue}");
                        break;
                    case Float:
                        float floatValue = (float)message[0];
                        if (!FloatParams.TryAdd(parameter.Name, floatValue))
                            FloatParams[parameter.Name] = floatValue;
                        PublishAvatarParameterChanged(parameter.Name, parameter.Input.Type, message);
                        //Console.WriteLine($"Parameter '{parameter.Name}' set to (Float): {floatValue}");
                        break;
                }
            }

            /// <summary>
            /// Converts an OSC parameter payload into the normalized avatar parameter change event.
            /// </summary>
            /// <param name="parameterName">Logical avatar parameter name without the OSC prefix.</param>
            /// <param name="parameterType">VRChat type label, or null when the type is not known.</param>
            /// <param name="message">OSC message that supplied the raw value.</param>
            private static void PublishAvatarParameterChanged(string parameterName, string? parameterType, OscMessage message)
            {
                var raw = GetArgumentOrDefault(message, 0);
                var floatValue = ConvertToSingle(raw);
                var payload = new VRChatAvatarParameterChangedEvent(
                    parameterName,
                    message.Address,
                    string.IsNullOrWhiteSpace(parameterType) ? "Unknown" : parameterType,
                    raw,
                    ConvertToBool(raw),
                    (int)MathF.Round(floatValue),
                    floatValue);
                InvokeSafely(OnAvatarParameterChanged, payload);
            }
            /// <summary>
            /// Attempts to read the latest cached floating-point value for an avatar parameter.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Cached value when present.</param>
            /// <returns><see langword="true"/> when the parameter has a cached float value.</returns>
            public static bool TryGetFloatParameter(string parameterName, out float value)
                => FloatParams.TryGetValue(parameterName, out value);

            /// <summary>
            /// Attempts to read the latest cached integer value for an avatar parameter.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Cached value when present.</param>
            /// <returns><see langword="true"/> when the parameter has a cached integer value.</returns>
            public static bool TryGetIntParameter(string parameterName, out int value)
                => IntParams.TryGetValue(parameterName, out value);

            /// <summary>
            /// Attempts to read the latest cached boolean value for an avatar parameter.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Cached value when present.</param>
            /// <returns><see langword="true"/> when the parameter has a cached boolean value.</returns>
            public static bool TryGetBoolParameter(string parameterName, out bool value)
                => BoolParams.TryGetValue(parameterName, out value);

            /// <summary>
            /// Reads a cached floating-point avatar parameter, returning zero when no value is cached.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <returns>The cached float value, or zero.</returns>
            public static float GetFloatParameter(string parameterName)
                => FloatParams.TryGetValue(parameterName, out float value) ? value : 0.0f;

            /// <summary>
            /// Reads a cached integer avatar parameter, returning zero when no value is cached.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <returns>The cached integer value, or zero.</returns>
            public static int GetIntParameter(string parameterName)
                => IntParams.TryGetValue(parameterName, out int value) ? value : 0;

            /// <summary>
            /// Reads a cached boolean avatar parameter, returning false when no true value is cached.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <returns>The cached boolean value, or false.</returns>
            public static bool GetBoolParameter(string parameterName)
                => BoolParams.TryGetValue(parameterName, out bool value) && value;

            /// <summary>
            /// Updates the integer parameter cache and sends the value to VRChat over OSC.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Integer value to cache and send.</param>
            public static void SetParameter(string parameterName, int value)
            {
                if (!IntParams.TryAdd(parameterName, value))
                    IntParams[parameterName] = value;
                Send(parameterName, value);
            }

            /// <summary>
            /// Updates the floating-point parameter cache and sends the value to VRChat over OSC.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Floating-point value to cache and send.</param>
            public static void SetParameter(string parameterName, float value)
            {
                if (!FloatParams.TryAdd(parameterName, value))
                    FloatParams[parameterName] = value;
                Send(parameterName, value);
            }

            /// <summary>
            /// Updates the boolean parameter cache and sends the value to VRChat over OSC.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name.</param>
            /// <param name="value">Boolean value to cache and send.</param>
            public static void SetParameter(string parameterName, bool value)
            {
                if (!BoolParams.TryAdd(parameterName, value))
                    BoolParams[parameterName] = value;
                Send(parameterName, value);
            }

            /// <summary>
            /// Sends a floating-point avatar parameter to VRChat when OSC sending is active.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name or address suffix.</param>
            /// <param name="value">Floating-point value to send.</param>
            private static void Send(string parameterName, float value)
            {
                if (!Sending)
                    return;

                parameterName = parameterName.Replace(" ", "_");
                SendOscMessage(new OscMessage($"{AvatarParameterPathPrefix}{parameterName}", value));
            }
            /// <summary>
            /// Sends an integer avatar parameter to VRChat when OSC sending is active.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name or address suffix.</param>
            /// <param name="value">Integer value to send.</param>
            private static void Send(string parameterName, int value)
            {
                if (!Sending)
                    return;

                parameterName = parameterName.Replace(" ", "_");
                SendOscMessage(new OscMessage($"{AvatarParameterPathPrefix}{parameterName}", value));
            }
            /// <summary>
            /// Sends a boolean avatar parameter to VRChat when OSC sending is active.
            /// </summary>
            /// <param name="parameterName">Avatar parameter name or address suffix.</param>
            /// <param name="value">Boolean value to send.</param>
            private static void Send(string parameterName, bool value)
            {
                if (!Sending)
                    return;

                parameterName = parameterName.Replace(" ", "_");
                SendOscMessage(new OscMessage($"{AvatarParameterPathPrefix}{parameterName}", value));
            }

            /// <summary>
            /// Sends the cached value for a configured avatar output parameter using its output address.
            /// </summary>
            /// <param name="parameter">Avatar parameter whose output definition should be sent.</param>
            private static void SendOutput(AvatarParameter parameter)
            {
                if (parameter?.Output is null ||
                    parameter.Name is null ||
                    parameter.Output.Address is null)
                    return;

                switch (parameter.Output.Type)
                {
                    case Int:
                        {
                            Send(parameter.Output.Address, IntParams.TryGetValue(parameter.Name, out int value) ? value : 0);
                        }
                        break;
                    case Bool:
                        {
                            Send(parameter.Output.Address, BoolParams.TryGetValue(parameter.Name, out bool value) && value);
                        }
                        break;
                    case Float:
                        {
                            Send(parameter.Output.Address, (float)(FloatParams.TryGetValue(parameter.Name, out float value) ? value : 0.0f));
                        }
                        break;
                }
            }
        }
    }
}
