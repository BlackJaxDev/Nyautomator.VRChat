using System.Collections.Generic;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Provides camera mode names known by the VRChat OSC helper for automation drop-downs.
/// </summary>
public class VRChatOscCameraModeOptionsProvider : IStringOptionsProvider
{
    /// <summary>
    /// Returns the available user camera modes.
    /// </summary>
    /// <returns>Camera mode option names.</returns>
    public IEnumerable<string> GetOptions() => VRChatHelper.OSC.CameraModeNames;
}
