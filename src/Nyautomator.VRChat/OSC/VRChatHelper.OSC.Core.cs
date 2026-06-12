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
    /// Shared VRChat helper namespace for OSC state and operations.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that sends, receives, caches, and dispatches VRChat OSC data.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Currently loaded avatar OSC configuration.
            /// </summary>
            private static AvatarConfig? Config;

            // Raised whenever a new avatar configuration is successfully parsed/applied.
            /// <summary>
            /// Occurs when a new avatar configuration has been parsed and applied.
            /// </summary>
            public static event Action? OnAvatarConfigChanged;

            // Raised when VRChat reports user camera state via OSC.
            /// <summary>
            /// Occurs when VRChat reports the user camera mode over OSC.
            /// </summary>
            public static event Action<VRChatCameraModeChangedEvent>? OnCameraModeChanged;

            /// <summary>
            /// Occurs when VRChat reports a user camera toggle over OSC.
            /// </summary>
            public static event Action<VRChatCameraToggleChangedEvent>? OnCameraToggleChanged;

            /// <summary>
            /// Occurs when VRChat reports a user camera slider over OSC.
            /// </summary>
            public static event Action<VRChatCameraSliderChangedEvent>? OnCameraSliderChanged;

            /// <summary>
            /// Occurs when VRChat reports the user camera pose over OSC.
            /// </summary>
            public static event Action<VRChatCameraPoseChangedEvent>? OnCameraPoseChanged;

            /// <summary>
            /// Occurs when VRChat reports a user camera action over OSC.
            /// </summary>
            public static event Action<VRChatCameraActionTriggeredEvent>? OnCameraActionTriggered;

            /// <summary>
            /// Occurs when an avatar parameter update is received and cached.
            /// </summary>
            public static event Action<VRChatAvatarParameterChangedEvent>? OnAvatarParameterChanged;

            /// <summary>
            /// Gets whether an avatar OSC configuration is currently loaded.
            /// </summary>
            public static bool IsAvatarConfigured => Config is not null;

            /// <summary>
            /// Gets parameter names from the current avatar configuration.
            /// </summary>
            public static IReadOnlyList<string> ParameterNames
                => Config?.Parameters?.Where(x => x.Name is not null).Select(p => p.Name!)?.ToList() ?? [];

            /// <summary>
            /// Gets the avatar id from the current OSC configuration.
            /// </summary>
            public static string AvatarId => Config?.Id ?? string.Empty;

            /// <summary>
            /// Gets the avatar display name from the current OSC configuration.
            /// </summary>
            public static string AvatarName => Config?.Name ?? string.Empty;

            /// <summary>
            /// Gets unique parameter names and their known types from the current avatar configuration.
            /// </summary>
            public static (string Name, string Type)[] CurrentParameters
            {
                get
                {
                    // Build a unique list of parameter names and their types, preferring Input.Type then Output.Type
                    if (Config?.Parameters is null)
                        return [];

                    List<(string Name, string Type)> list = [];
                    foreach (var p in Config.Parameters)
                    {
                        if (p?.Name is null)
                            continue;

                        var type = p.Input?.Type ?? p.Output?.Type;
                        if (type is null)
                            continue;

                        if (!list.Any(x => string.Equals(x.Name, p.Name, StringComparison.Ordinal)))
                            list.Add((p.Name, type));
                    }
                    return [.. list];
                }
            }
        }
    }
}
