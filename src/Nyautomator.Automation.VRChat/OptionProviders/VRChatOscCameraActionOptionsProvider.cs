using System.Collections.Generic;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Provides camera action names known by the VRChat OSC helper for automation drop-downs.
/// </summary>
public class VRChatOscCameraActionOptionsProvider : IStringOptionsProvider
{
    /// <summary>
    /// Returns the available user camera actions, such as capture or close.
    /// </summary>
    /// <returns>Camera action option names.</returns>
    public IEnumerable<string> GetOptions() => VRChatHelper.OSC.CameraActionNames;
}
