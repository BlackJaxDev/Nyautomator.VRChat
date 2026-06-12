using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Flow node that requests one VRChat avatar by id through the authenticated integration host.
/// </summary>
[AutomationOutputs("out", "success", "issuccess", "statuscode", "error", "rawjson", "avatarid", "avatarname")]
public sealed class VRChatGetAvatarByIdFlow : FlowNodeType
{
    /// <summary>
    /// Gets the automation registry id for the single-avatar lookup flow.
    /// </summary>
    public override string Id => "VRChat.Api.GetAvatar";

    /// <summary>
    /// Gets the label shown for this flow in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Get Avatar By Id";

    /// <summary>
    /// Gets the user-facing explanation of the avatar lookup request.
    /// </summary>
    public override string Description => "Gets one VRChat avatar by id.";

    /// <summary>
    /// Gets or sets the VRChat avatar id to request.
    /// </summary>
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>
    /// Executes the asynchronous lookup synchronously for the automation engine.
    /// </summary>
    /// <returns><see langword="null"/> because results are published through <see cref="FlowNodeType.Outputs"/>.</returns>
    public override string? Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        return null;
    }

    /// <summary>
    /// Requests the configured avatar and populates metadata, raw JSON, and parsed JSON outputs.
    /// </summary>
    /// <returns>A task that completes after outputs have been updated.</returns>
    public async Task ExecuteAsync()
    {
        SetDefaults();
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

        var response = await sender(new AuthenticatedIntegrationRequest("VRChat", "GET", $"/avatars/{Uri.EscapeDataString(AvatarId.Trim())}", null, null, "application/json", 10000), CancellationToken.None).ConfigureAwait(false);
        Outputs["Success"] = response.Success;
        Outputs["IsSuccess"] = response.IsSuccess;
        Outputs["StatusCode"] = response.StatusCode;
        Outputs["Error"] = response.Error ?? string.Empty;
        Outputs["RawJson"] = response.Body ?? string.Empty;

        if (!response.Success || string.IsNullOrWhiteSpace(response.Body))
            return;

        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement.Clone();
        Outputs["out"] = root;
        Outputs["AvatarId"] = root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        Outputs["AvatarName"] = root.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// Resolves the type exposed by each supported avatar lookup output handle.
    /// </summary>
    /// <param name="outputHandle">Output handle requested by the automation graph.</param>
    /// <returns>The .NET type expected for that output handle.</returns>
    public override Type? GetOutputType(string? outputHandle)
    {
        var handle = AutomationValueHelper.NormalizeValueHandle(outputHandle).ToLowerInvariant();
        return ResolveOutputTypeToken(handle switch
        {
            "" or "out" => "object:System.Text.Json.JsonElement",
            "success" or "issuccess" => "boolean",
            "statuscode" => "int32",
            "avatarid" or "avatarname" or "error" or "rawjson" => "string",
            _ => "object"
        }, fallbackToObject: false);
    }

    /// <summary>
    /// Resets lookup outputs before validation or a request is attempted.
    /// </summary>
    private void SetDefaults()
    {
        Outputs.Clear();
        Outputs["Success"] = false;
        Outputs["out"] = null;
        Outputs["IsSuccess"] = false;
        Outputs["StatusCode"] = 0;
        Outputs["Error"] = string.Empty;
        Outputs["RawJson"] = string.Empty;
        Outputs["AvatarId"] = AvatarId ?? string.Empty;
        Outputs["AvatarName"] = string.Empty;
    }
}
