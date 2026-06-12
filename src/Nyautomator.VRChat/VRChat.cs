using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace Nyautomator
{
    /// <summary>
    /// Shared VRChat helpers for locating local VRChat data and interacting with the local client process.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Gets the VRChat LocalLow data directory for the current Windows profile.
        /// </summary>
        public static readonly string DataDir = Path.Combine(GetLocalLowPath(), "VRChat", "VRChat");

        /// <summary>
        /// Gets the default OSC configuration directory under the current profile's VRChat data directory.
        /// </summary>
        public static readonly string OSCDir = Path.Combine(DataDir, "OSC");

        /// <summary>
        /// Gets the LocalLow folder path by walking from the current user's LocalAppData folder.
        /// </summary>
        /// <returns>The current user's LocalLow path.</returns>
        public static string GetLocalLowPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "..", "LocalLow");

        /// <summary>
        /// Enables VRChat OSC-related registry toggles that are currently set to disabled.
        /// </summary>
        /// <returns><see langword="true"/> when at least one OSC registry value was changed to enabled.</returns>
        [SupportedOSPlatform("windows")]
        public static bool ForceEnableOSCWindows()
        {
            // Set all registry keys containing osc in the name to 1 in Computer\HKEY_CURRENT_USER\Software\VRChat\VRChat
            bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            if (!isWindows)
                return false;

            var regKey = Registry.CurrentUser.OpenSubKey("Software\\VRChat\\VRChat", true);
            if (regKey == null)
                return true;

            var keys = regKey.GetValueNames().Where(x => x.Contains("osc", StringComparison.CurrentCultureIgnoreCase));

            bool wasOscForced = false;
            foreach (var key in keys)
            {
                if (regKey.GetValue(key) is int v && v == 0)
                {
                    // Osc is likely not enabled
                    regKey.SetValue(key, 1);
                    wasOscForced = true;
                }
            }

            return wasOscForced;
        }

        /// <summary>
        /// Checks whether a process named VRChat is currently running.
        /// </summary>
        /// <returns><see langword="true"/> when a VRChat process is visible to the current user.</returns>
        public static bool IsVRChatRunning() => Process.GetProcesses().Any(x => x.ProcessName == "VRChat");
    }
}
