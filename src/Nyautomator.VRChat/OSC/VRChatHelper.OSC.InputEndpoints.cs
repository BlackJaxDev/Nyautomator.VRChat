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
    /// Shared VRChat helper namespace for built-in OSC input endpoints.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper for sending VRChat built-in input endpoint values.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Built-in VRChat OSC input endpoint definitions and their ranges.
            /// </summary>
            private static readonly (string Name, OscInputEndpointDefinition Definition)[] InputEndpointDefinitions =
            [
                // Axes (float -1..1) ------------------------------------------------------------
                ("Vertical", new OscInputEndpointDefinition("Vertical", -1f, 1f, 0f, false, "Move forwards (1) or backwards (-1).")),
                ("Horizontal", new OscInputEndpointDefinition("Horizontal", -1f, 1f, 0f, false, "Move right (1) or left (-1).")),
                ("LookHorizontal", new OscInputEndpointDefinition("LookHorizontal", -1f, 1f, 0f, false, "Smooth look left/right; snap-turn in VR when comfort turning is enabled.")),
                ("UseAxisRight", new OscInputEndpointDefinition("UseAxisRight", -1f, 1f, 0f, false, "Axis for using held item (per VRChat docs; may not be active).")),
                ("GrabAxisRight", new OscInputEndpointDefinition("GrabAxisRight", -1f, 1f, 0f, false, "Axis for grabbing an item in the right hand.")),
                ("MoveHoldFB", new OscInputEndpointDefinition("MoveHoldFB", -1f, 1f, 0f, false, "Move a held object forward/backward.")),
                ("SpinHoldCwCcw", new OscInputEndpointDefinition("SpinHoldCwCcw", -1f, 1f, 0f, false, "Spin a held object clockwise/counter-clockwise.")),
                ("SpinHoldUD", new OscInputEndpointDefinition("SpinHoldUD", -1f, 1f, 0f, false, "Spin a held object up/down.")),
                ("SpinHoldLR", new OscInputEndpointDefinition("SpinHoldLR", -1f, 1f, 0f, false, "Spin a held object left/right.")),

                // Buttons (0 or 1) --------------------------------------------------------------
                ("MoveForward", new OscInputEndpointDefinition("MoveForward", 0f, 1f, 1f, true, "Move forward while pressed.")),
                ("MoveBackward", new OscInputEndpointDefinition("MoveBackward", 0f, 1f, 1f, true, "Move backward while pressed.")),
                ("MoveLeft", new OscInputEndpointDefinition("MoveLeft", 0f, 1f, 1f, true, "Strafe left while pressed.")),
                ("MoveRight", new OscInputEndpointDefinition("MoveRight", 0f, 1f, 1f, true, "Strafe right while pressed.")),
                ("LookLeft", new OscInputEndpointDefinition("LookLeft", 0f, 1f, 1f, true, "Turn left while pressed (snap-turn in VR with comfort turning).")),
                ("LookRight", new OscInputEndpointDefinition("LookRight", 0f, 1f, 1f, true, "Turn right while pressed (snap-turn in VR with comfort turning).")),
                ("Jump", new OscInputEndpointDefinition("Jump", 0f, 1f, 1f, true, "Trigger a jump (world permitting).")),
                ("Run", new OscInputEndpointDefinition("Run", 0f, 1f, 1f, true, "Hold to run if supported by the world.")),
                ("ComfortLeft", new OscInputEndpointDefinition("ComfortLeft", 0f, 1f, 1f, true, "VR-only snap-turn to the left.")),
                ("ComfortRight", new OscInputEndpointDefinition("ComfortRight", 0f, 1f, 1f, true, "VR-only snap-turn to the right.")),
                ("DropRight", new OscInputEndpointDefinition("DropRight", 0f, 1f, 1f, true, "VR-only: drop the right-hand item.")),
                ("UseRight", new OscInputEndpointDefinition("UseRight", 0f, 1f, 1f, true, "VR-only: use the highlighted item in the right hand.")),
                ("GrabRight", new OscInputEndpointDefinition("GrabRight", 0f, 1f, 1f, true, "VR-only: grab with the right hand.")),
                ("DropLeft", new OscInputEndpointDefinition("DropLeft", 0f, 1f, 1f, true, "VR-only: drop the left-hand item.")),
                ("UseLeft", new OscInputEndpointDefinition("UseLeft", 0f, 1f, 1f, true, "VR-only: use the highlighted item in the left hand.")),
                ("GrabLeft", new OscInputEndpointDefinition("GrabLeft", 0f, 1f, 1f, true, "VR-only: grab with the left hand.")),
                ("PanicButton", new OscInputEndpointDefinition("PanicButton", 0f, 1f, 1f, true, "Activate Safe Mode.")),
                ("QuickMenuToggleLeft", new OscInputEndpointDefinition("QuickMenuToggleLeft", 0f, 1f, 1f, true, "Toggle the quick menu (left controller).")),
                ("QuickMenuToggleRight", new OscInputEndpointDefinition("QuickMenuToggleRight", 0f, 1f, 1f, true, "Toggle the quick menu (right controller).")),
                ("Voice", new OscInputEndpointDefinition("Voice", 0f, 1f, 1f, true, "Toggle Voice - the action will depend on whether \"Toggle Voice\" is turned on in your Settings. If on, changing from 0 to 1 will toggle mute. After, set it to 0 again. (While it's 1, you will be unable to use your controller or keyboard to toggle mute) If off, it functions like Push-To-Mute - 0 is muted, 1 is unmuted.")),
            ];

            /// <summary>
            /// Lookup table for built-in input endpoints using case-insensitive endpoint names.
            /// </summary>
            private static readonly Dictionary<string, OscInputEndpointDefinition> BuiltInInputEndpoints =
                InputEndpointDefinitions.ToDictionary(static entry => entry.Name, static entry => entry.Definition, StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets the built-in VRChat input endpoint names.
            /// </summary>
            public static IEnumerable<string> InputEndpointNames => InputEndpointDefinitions.Select(static entry => entry.Name);

            /// <summary>
            /// Gets option labels containing endpoint name, type, range, and description.
            /// </summary>
            public static IEnumerable<string> InputEndpointOptions
            {
                get
                {
                    foreach (var (Name, Definition) in InputEndpointDefinitions)
                    {
                        var definition = Definition;
                        var typeLabel = definition.IsButton ? "Button" : "Axis";
                        var rangeLabel = definition.IsButton
                            ? "0 or 1"
                            : string.Join("..", FormatRange(definition.Min), FormatRange(definition.Max));
                        var description = definition.Description ?? string.Empty;
                        var label = string.Join('|', Name, typeLabel, rangeLabel, description);
                        yield return string.Concat(label, "::", Name);
                    }
                }
            }

            /// <summary>
            /// Formats an endpoint range value with invariant culture for option labels.
            /// </summary>
            /// <param name="value">Range value to format.</param>
            /// <returns>Formatted numeric range value.</returns>
            private static string FormatRange(float value)
                => value.ToString("0.###", CultureInfo.InvariantCulture);

            /// <summary>
            /// Resolves a built-in input endpoint by name or full /input address suffix.
            /// </summary>
            /// <param name="endpointName">Endpoint name or /input address.</param>
            /// <param name="endpoint">Resolved endpoint definition when found.</param>
            /// <returns><see langword="true"/> when a built-in endpoint exists.</returns>
            public static bool TryGetInputEndpoint(string? endpointName, out OscInputEndpointDefinition endpoint)
            {
                endpoint = default;
                if (string.IsNullOrWhiteSpace(endpointName))
                    return false;

                var key = NormalizeInputEndpointKey(endpointName);
                return BuiltInInputEndpoints.TryGetValue(key, out endpoint);
            }

            /// <summary>
            /// Sends a float value to a VRChat input endpoint, clamping and button-normalizing known endpoints.
            /// </summary>
            /// <param name="endpointName">Endpoint name or /input address.</param>
            /// <param name="value">Value to send.</param>
            public static void SendInput(string? endpointName, float value)
            {
                if (!Sending || string.IsNullOrWhiteSpace(endpointName))
                    return;

                var key = NormalizeInputEndpointKey(endpointName);
                var suffix = key;
                var payload = value;

                if (BuiltInInputEndpoints.TryGetValue(key, out var endpoint))
                {
                    suffix = endpoint.AddressSuffix;
                    payload = Math.Clamp(value, endpoint.Min, endpoint.Max);
                    if (endpoint.IsButton)
                        payload = payload >= 0.5f ? 1f : 0f;
                }

                SendOscMessage(new OscMessage($"/input/{suffix}", payload));
            }

            /// <summary>
            /// Sends a boolean button value to a VRChat input endpoint.
            /// </summary>
            /// <param name="endpointName">Endpoint name or /input address.</param>
            /// <param name="pressed">Button state to send.</param>
            public static void SendInput(string? endpointName, bool pressed)
                => SendInput(endpointName, pressed ? 1f : 0f);

            /// <summary>
            /// Sends an integer value to a VRChat input endpoint.
            /// </summary>
            /// <param name="endpointName">Endpoint name or /input address.</param>
            /// <param name="value">Integer value to send.</param>
            public static void SendInput(string? endpointName, int value)
                => SendInput(endpointName, (float)value);

            /// <summary>
            /// Normalizes an endpoint key by trimming, removing /input prefix, and removing spaces.
            /// </summary>
            /// <param name="endpointName">Raw endpoint name.</param>
            /// <returns>Normalized endpoint lookup key.</returns>
            private static string NormalizeInputEndpointKey(string endpointName)
            {
                var key = endpointName.Trim();
                if (key.StartsWith("/input/", StringComparison.OrdinalIgnoreCase))
                    key = key[7..];

                return key.Replace(" ", string.Empty);
            }

            /// <summary>
            /// Metadata for a built-in VRChat OSC input endpoint.
            /// </summary>
            /// <param name="AddressSuffix">OSC address suffix under /input.</param>
            /// <param name="Min">Minimum accepted numeric value.</param>
            /// <param name="Max">Maximum accepted numeric value.</param>
            /// <param name="Default">Default value used by automation callers when unspecified.</param>
            /// <param name="IsButton">Whether the endpoint should be normalized to 0 or 1.</param>
            /// <param name="Description">Human-readable endpoint behavior description.</param>
            public readonly record struct OscInputEndpointDefinition(
                string AddressSuffix,
                float Min,
                float Max,
                float Default,
                bool IsButton,
                string Description);
        }
    }
}
