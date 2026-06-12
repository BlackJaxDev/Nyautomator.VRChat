using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Describes how many times a dolly track should play once started.
/// </summary>
public enum VRChatDollyRunMode
{
    /// <summary>
    /// Plays the track one time.
    /// </summary>
    Once,

    /// <summary>
    /// Plays the track for a configured number of loops.
    /// </summary>
    Count,

    /// <summary>
    /// Plays until an explicit stop request cancels the playback.
    /// </summary>
    UntilStopped
}
