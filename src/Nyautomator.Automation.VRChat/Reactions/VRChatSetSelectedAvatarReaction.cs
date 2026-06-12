using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Reaction node that selects a VRChat avatar through the authenticated integration host.
/// </summary>
[AutomationOutputs("out", "success", "issuccess", "statuscode", "error", "rawjson")]
public sealed class VRChatSetSelectedAvatarReaction : ReactionType, IHasOutputs, IAsyncReaction
{
    /// <summary>
    /// Synchronizes the process-wide avatar selection cooldown check.
    /// </summary>
    private static readonly object SelectCooldownLock = new();

    /// <summary>
    /// Stores the last UTC timestamp at which this process attempted avatar selection.
    /// </summary>
    private static DateTimeOffset LastSelectUtc = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the automation registry id for the selected-avatar reaction.
    /// </summary>
    public override string Id => "VRChat.Api.SetSelectedAvatar";

    /// <summary>
    /// Gets the label shown for this reaction in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Set Selected Avatar";

    /// <summary>
    /// Gets the user-facing explanation of the authenticated VRChat avatar change.
    /// </summary>
    public override string Description => "Switches the logged-in VRChat account into the given avatar.";

    /// <summary>
    /// Gets or sets the VRChat avatar id to select.
    /// </summary>
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum seconds between avatar selection attempts in this process.
    /// </summary>
    public int CooldownSeconds { get; set; } = 30;

    /// <summary>
    /// Gets output values populated after the selection request is attempted.
    /// </summary>
    public IDictionary<string, object?> Outputs { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Executes the asynchronous avatar selection synchronously for the automation engine.
    /// </summary>
    public override void Execute()
        => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>
    /// Validates the configured avatar id, enforces cooldown, and sends the select request through the integration host.
    /// </summary>
    /// <returns>A task that completes after outputs have been updated.</returns>
    public async Task ExecuteAsync()
    {
        Outputs.Clear();
        Outputs["Success"] = false;
        Outputs["out"] = false;
        Outputs["IsSuccess"] = false;
        Outputs["StatusCode"] = 0;
        Outputs["Error"] = string.Empty;
        Outputs["RawJson"] = string.Empty;

        if (string.IsNullOrWhiteSpace(AvatarId))
        {
            Outputs["Error"] = "AvatarId is required.";
            return;
        }

        var sender = AutomationHost.SendAuthenticatedIntegrationRequest;
        if (sender is null)
        {
            Outputs["Error"] = "Authenticated integration request host delegate is not available.";
            return;
        }

        var cooldown = Math.Max(0, CooldownSeconds);
        if (cooldown > 0)
        {
            lock (SelectCooldownLock)
            {
                var elapsed = DateTimeOffset.UtcNow - LastSelectUtc;
                if (elapsed < TimeSpan.FromSeconds(cooldown))
                {
                    Outputs["Error"] = $"VRChat avatar select is cooling down for {Math.Ceiling((TimeSpan.FromSeconds(cooldown) - elapsed).TotalSeconds)} more second(s).";
                    return;
                }

                LastSelectUtc = DateTimeOffset.UtcNow;
            }
        }

        var response = await sender(new AuthenticatedIntegrationRequest("VRChat", "PUT", $"/avatars/{Uri.EscapeDataString(AvatarId.Trim())}/select", null, null, "application/json", 10000), CancellationToken.None).ConfigureAwait(false);
        Outputs["Success"] = response.Success && response.IsSuccess;
        Outputs["out"] = response.Success && response.IsSuccess;
        Outputs["IsSuccess"] = response.IsSuccess;
        Outputs["StatusCode"] = response.StatusCode;
        Outputs["Error"] = response.Error ?? string.Empty;
        Outputs["RawJson"] = response.Body ?? string.Empty;
    }

    /// <summary>
    /// Resolves the type exposed by each supported avatar-selection output handle.
    /// </summary>
    /// <param name="outputHandle">Output handle requested by the automation graph.</param>
    /// <returns>The .NET type expected for that output handle.</returns>
    public override Type? GetOutputType(string? outputHandle)
    {
        var handle = AutomationValueHelper.NormalizeValueHandle(outputHandle).ToLowerInvariant();
        return ResolveOutputTypeToken(handle switch
        {
            "" or "out" or "success" or "issuccess" => "boolean",
            "statuscode" => "int32",
            "error" or "rawjson" => "string",
            _ => "boolean"
        }, fallbackToObject: false);
    }
}
