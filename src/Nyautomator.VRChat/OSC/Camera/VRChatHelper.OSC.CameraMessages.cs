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
    /// Shared VRChat helper namespace for received camera OSC messages.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that translates user camera OSC messages into cached state and events.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Handles a received /usercamera OSC message and dispatches the matching camera event.
            /// </summary>
            /// <param name="message">OSC message received from VRChat.</param>
            private static void HandleCameraMessage(OscMessage message)
            {
                var address = message.Address;
                if (string.IsNullOrWhiteSpace(address))
                    return;

                if (string.Equals(address, CameraModeAddress, StringComparison.OrdinalIgnoreCase))
                {
                    var raw = GetArgumentOrDefault(message, 0);
                    var numeric = ConvertToSingle(raw);
                    var mode = (int)MathF.Round(numeric);
                    var modeName = CameraModeNameByValue.TryGetValue(mode, out var name) ? name : string.Empty;
                    UpdateCameraMode(mode, modeName);
                    var payload = new VRChatCameraModeChangedEvent(mode, modeName, address, numeric);
                    InvokeSafely(OnCameraModeChanged, payload);
                    return;
                }

                if (string.Equals(address, CameraPoseAddress, StringComparison.OrdinalIgnoreCase))
                {
                    var rawValues = ReadFloatArguments(message, 7);
                    var position = new Vector3(GetRaw(rawValues, 0), GetRaw(rawValues, 1), GetRaw(rawValues, 2));

                    Vector3 eulerDegrees;
                    Quaternion rotation;

                    if (rawValues.Count >= 7)
                    {
                        rotation = SafeNormalize(new Quaternion(
                            GetRaw(rawValues, 3),
                            GetRaw(rawValues, 4),
                            GetRaw(rawValues, 5),
                            GetRaw(rawValues, 6)));
                        eulerDegrees = QuaternionToEulerDegrees(rotation);
                    }
                    else
                    {
                        eulerDegrees = new Vector3(GetRaw(rawValues, 3), GetRaw(rawValues, 4), GetRaw(rawValues, 5));
                        rotation = EulerDegreesToQuaternion(eulerDegrees);
                    }

                    var poseSnapshot = new VRChatCameraPoseSnapshot(position, eulerDegrees, rotation, rawValues, DateTime.UtcNow);
                    UpdateCameraPose(poseSnapshot);
                    var posePayload = new VRChatCameraPoseChangedEvent(position, rotation, address, eulerDegrees, rawValues);
                    InvokeSafely(OnCameraPoseChanged, posePayload);
                    return;
                }

                if (CameraToggleNameByAddress.TryGetValue(address, out var toggleName))
                {
                    var rawToggle = GetArgumentOrDefault(message, 0);
                    var enabled = ConvertToBool(rawToggle);
                    UpdateCameraToggle(toggleName, enabled);
                    var togglePayload = new VRChatCameraToggleChangedEvent(toggleName, enabled, address, ConvertToSingle(rawToggle));
                    InvokeSafely(OnCameraToggleChanged, togglePayload);
                    return;
                }

                if (CameraSliderNameByAddress.TryGetValue(address, out var sliderName))
                {
                    var value = ConvertToSingle(GetArgumentOrDefault(message, 0));
                    UpdateCameraSlider(sliderName, value);
                    var sliderPayload = new VRChatCameraSliderChangedEvent(sliderName, value, address);
                    InvokeSafely(OnCameraSliderChanged, sliderPayload);
                    return;
                }

                if (CameraActionNameByAddress.TryGetValue(address, out var actionName))
                {
                    var value = ConvertToSingle(GetArgumentOrDefault(message, 0));
                    var actionPayload = new VRChatCameraActionTriggeredEvent(actionName, value, address);
                    InvokeSafely(OnCameraActionTriggered, actionPayload);
                }
            }

        }
    }
}
