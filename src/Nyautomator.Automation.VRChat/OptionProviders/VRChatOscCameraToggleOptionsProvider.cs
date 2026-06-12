using System.Collections.Generic;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Provides camera toggle names known by the VRChat OSC helper for automation drop-downs.
/// </summary>
public class VRChatOscCameraToggleOptionsProvider : IStringOptionsProvider
{
    /// <summary>
    /// Returns the available user camera toggle controls.
    /// </summary>
    /// <returns>Camera toggle option names.</returns>
    public IEnumerable<string> GetOptions() => VRChatHelper.OSC.CameraToggleNames;
}
