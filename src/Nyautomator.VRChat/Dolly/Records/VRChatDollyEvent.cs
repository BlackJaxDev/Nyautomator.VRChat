using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

/// <summary>
/// Event emitted by the dolly runtime for capture, track, and playback changes.
/// </summary>
/// <param name="Type">Machine-readable event type.</param>
/// <param name="TimestampUtc">UTC time when the event was created.</param>
/// <param name="TrackId">Related track identifier when one is available.</param>
/// <param name="Message">Human-readable event message.</param>
/// <param name="Payload">Optional structured event details.</param>
public sealed record VRChatDollyEvent(
    string Type,
    DateTime TimestampUtc,
    string? TrackId,
    string Message,
    object? Payload = null);
