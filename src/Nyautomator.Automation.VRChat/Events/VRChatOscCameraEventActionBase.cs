using System;
using Nyautomator;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Base automation trigger for typed VRChat OSC camera events.
/// </summary>
/// <typeparam name="TPayload">The VRChat camera payload type emitted by the OSC helper.</typeparam>
public abstract class VRChatOscCameraEventActionBase<TPayload> : ActionType, IExternalPayloadAction
{
    /// <summary>
    /// Determines whether a typed camera payload passes any node-specific filters.
    /// </summary>
    /// <param name="payload">Typed camera event payload.</param>
    /// <returns><see langword="true"/> when this action should fire for the payload.</returns>
    protected virtual bool ShouldHandle(TPayload payload) => true;

    /// <summary>
    /// Accepts only payloads that match <typeparamref name="TPayload"/> and pass the typed filter.
    /// </summary>
    /// <param name="payload">External payload delivered by the automation engine.</param>
    /// <returns><see langword="true"/> when the payload should fire this action.</returns>
    bool IExternalPayloadAction.ShouldHandlePayload(object? payload)
        => payload is TPayload typed && ShouldHandle(typed);

    /// <summary>
    /// Reports that camera event outputs expose the raw typed payload.
    /// </summary>
    /// <param name="outputHandle">Output handle requested by the automation graph.</param>
    /// <returns>The payload type available from this event node.</returns>
    public override Type? GetOutputType(string? outputHandle)
        => typeof(TPayload);
}
