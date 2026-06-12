using System.Collections.Generic;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Provides built-in VRChat OSC input endpoint names for automation drop-downs.
/// </summary>
public class VRChatOscInputEndpointOptionsProvider : IStringOptionsProvider
{
    /// <summary>
    /// Returns OSC input endpoint options such as movement axes and button inputs.
    /// </summary>
    /// <returns>Input endpoint option names.</returns>
    public IEnumerable<string> GetOptions()
    {
        return VRChatHelper.OSC.InputEndpointOptions;
    }
}
