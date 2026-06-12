using Nyautomator;

namespace NyautomatorUI.Server.Automation
{
    /// <summary>
    /// Provides parameter names from the currently loaded VRChat OSC avatar configuration.
    /// </summary>
    public class VRChatOscParameterOptionsProvider : IStringOptionsProvider
    {
        /// <summary>
        /// Returns parameter names cached by the VRChat OSC helper.
        /// </summary>
        /// <returns>Avatar parameter option names.</returns>
        public IEnumerable<string> GetOptions()
        {
            return VRChatHelper.OSC.ParameterNames;
        }
    }
}
