using System.ComponentModel;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Base automation trigger for dolly events emitted by the VRChat dolly runtime.
/// </summary>
public abstract class VRChatDollyEventActionBase : ActionType, IExternalPayloadAction
{
    /// <summary>
    /// Gets the runtime event type string this action accepts before optional track filtering.
    /// </summary>
    protected abstract string DollyEventType { get; }

    /// <summary>
    /// Gets or sets the optional dolly track id that must match the incoming event.
    /// </summary>
    [Description("Optional track id filter. Leave blank to accept all tracks.")]
    public string? TrackId { get; set; }

    /// <summary>
    /// Accepts only dolly payloads whose runtime type and optional track id match this action.
    /// </summary>
    /// <param name="payload">External payload delivered by the automation engine.</param>
    /// <returns><see langword="true"/> when the payload should fire this action.</returns>
    bool IExternalPayloadAction.ShouldHandlePayload(object? payload)
        => payload is VRChatDollyEvent evt && 
            string.Equals(evt.Type, DollyEventType, StringComparison.OrdinalIgnoreCase) && 
            (string.IsNullOrWhiteSpace(TrackId) || string.Equals(TrackId.Trim(), evt.TrackId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Reports that dolly event outputs expose the raw <see cref="VRChatDollyEvent"/> payload.
    /// </summary>
    /// <param name="outputHandle">Output handle requested by the automation graph.</param>
    /// <returns>The payload type available from this event node.</returns>
    public override Type? GetOutputType(string? outputHandle)
        => typeof(VRChatDollyEvent);
}
