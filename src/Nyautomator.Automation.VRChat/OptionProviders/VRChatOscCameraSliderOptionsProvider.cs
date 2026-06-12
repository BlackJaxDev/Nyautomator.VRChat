using System.Collections.Generic;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Provides camera slider names known by the VRChat OSC helper for automation drop-downs.
/// </summary>
public class VRChatOscCameraSliderOptionsProvider : IStringOptionsProvider
{
    /// <summary>
    /// Returns the available user camera slider controls.
    /// </summary>
    /// <returns>Camera slider option names.</returns>
    public IEnumerable<string> GetOptions() => VRChatHelper.OSC.CameraSliderNames;
}
