using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Base flow node for VRChat avatar list requests that return JSON arrays through automation outputs.
/// </summary>
[AutomationOutputs("out", "success", "issuccess", "statuscode", "error", "rawjson", "count")]
public abstract class VRChatAvatarListFlowBase : FlowNodeType
{
    /// <summary>
    /// Gets or sets the maximum number of avatars to fetch; values less than one use the default safety limit.
    /// </summary>
    public int MaxResults { get; set; } = 0;

    /// <summary>
    /// Gets or sets the starting VRChat API offset for the first page.
    /// </summary>
    public int Offset { get; set; }

    /// <summary>
    /// Gets or sets the optional VRChat avatar platform query value.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Fetches the concrete avatar list response for the derived flow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token passed by the caller.</param>
    /// <returns>The authenticated VRChat integration response to expose through outputs.</returns>
    protected abstract Task<AuthenticatedIntegrationResponse> FetchAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes the asynchronous avatar request synchronously for the automation engine.
    /// </summary>
    /// <returns><see langword="null"/> because results are published through <see cref="FlowNodeType.Outputs"/>.</returns>
    public override string? Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        return null;
    }

    /// <summary>
    /// Runs the avatar request, records host or request errors, and populates output handles.
    /// </summary>
    /// <returns>A task that completes after outputs have been updated.</returns>
    public async Task ExecuteAsync()
    {
        SetDefaults();
        var sender = AutomationHost.SendAuthenticatedIntegrationRequest;
        if (sender is null)
        {
            Outputs["Error"] = "Authenticated integration request host delegate is not available.";
            return;
        }

        try
        {
            var response = await FetchAsync(CancellationToken.None).ConfigureAwait(false);
            PopulateResponse(response);
        }
        catch (Exception ex)
        {
            Outputs["Error"] = ex.Message;
        }
    }

    /// <summary>
    /// Resolves the type exposed by each supported avatar-list output handle.
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
            "statuscode" or "count" => "int32",
            "error" or "rawjson" => "string",
            _ => "object"
        }, fallbackToObject: false);
    }

    /// <summary>
    /// Builds a GET request for the authenticated VRChat integration endpoint.
    /// </summary>
    /// <param name="path">VRChat API path to request.</param>
    /// <param name="query">Query string without the leading question mark.</param>
    /// <returns>An authenticated integration request for the VRChat connector.</returns>
    protected static AuthenticatedIntegrationRequest BuildRequest(string path, string query)
        => new("VRChat", "GET", path, query, null, "application/json", 15000);

    /// <summary>
    /// Fetches one or more VRChat API pages and merges array responses into a single JSON array body.
    /// </summary>
    /// <param name="path">VRChat API path to request.</param>
    /// <param name="extra">Additional query pairs to append to each page request.</param>
    /// <returns>The first failed response, or a synthetic response containing the merged JSON array.</returns>
    protected async Task<AuthenticatedIntegrationResponse> FetchPagedAsync(string path, params (string Key, string? Value)[] extra)
    {
        var sender = AutomationHost.SendAuthenticatedIntegrationRequest!;
        var pageSize = 50;
        var offset = Math.Max(0, Offset);
        var limit = MaxResults <= 0 ? 7500 : Math.Max(1, MaxResults);
        var merged = new List<JsonElement>();
        AuthenticatedIntegrationResponse? lastResponse = null;

        while (merged.Count < limit)
        {
            var take = Math.Min(pageSize, limit - merged.Count);
            var response = await sender(BuildRequest(path, BuildPagingQuery(take, offset, extra)), CancellationToken.None).ConfigureAwait(false);
            lastResponse = response;
            if (!response.Success || !response.IsSuccess || string.IsNullOrWhiteSpace(response.Body))
                return response;

            using var document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return response;

            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                merged.Add(item.Clone());
                count++;
            }

            if (count < take || count == 0)
                break;

            offset += count;
        }

        var body = "[" + string.Join(",", merged.Select(static item => item.GetRawText())) + "]";
        return new AuthenticatedIntegrationResponse(true, lastResponse?.IsSuccess ?? true, lastResponse?.StatusCode ?? 200, body, lastResponse?.Headers ?? string.Empty, null);
    }

    /// <summary>
    /// Creates the VRChat avatar list query string with clamped page size, non-negative offset, and optional filters.
    /// </summary>
    /// <param name="pageSize">Requested page size before clamping to VRChat's page maximum.</param>
    /// <param name="offset">Requested starting offset before non-negative normalization.</param>
    /// <param name="extra">Additional query pairs that are ignored when either side is blank.</param>
    /// <returns>An escaped query string suitable for <see cref="BuildRequest"/>.</returns>
    protected string BuildPagingQuery(int pageSize, int offset, params (string Key, string? Value)[] extra)
    {
        var pairs = new List<string>
        {
            $"n={Math.Clamp(pageSize, 1, 50)}",
            $"offset={Math.Max(0, offset)}"
        };
        if (!string.IsNullOrWhiteSpace(Platform))
            pairs.Add($"platform={Uri.EscapeDataString(Platform.Trim())}");
        foreach (var (key, value) in extra)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value.Trim())}");
        }
        return string.Join("&", pairs);
    }

    /// <summary>
    /// Resets common avatar-list outputs before a request is attempted.
    /// </summary>
    protected void SetDefaults()
    {
        Outputs.Clear();
        Outputs["Success"] = false;
        Outputs["out"] = null;
        Outputs["IsSuccess"] = false;
        Outputs["StatusCode"] = 0;
        Outputs["Error"] = string.Empty;
        Outputs["RawJson"] = string.Empty;
        Outputs["Count"] = 0;
    }

    /// <summary>
    /// Copies response metadata and parsed JSON into the common avatar-list output handles.
    /// </summary>
    /// <param name="response">Response returned by the authenticated VRChat integration.</param>
    protected void PopulateResponse(AuthenticatedIntegrationResponse response)
    {
        Outputs["Success"] = response.Success;
        Outputs["IsSuccess"] = response.IsSuccess;
        Outputs["StatusCode"] = response.StatusCode;
        Outputs["Error"] = response.Error ?? string.Empty;
        Outputs["RawJson"] = response.Body ?? string.Empty;

        if (!response.Success || string.IsNullOrWhiteSpace(response.Body))
            return;

        try
        {
            using var document = JsonDocument.Parse(response.Body);
            var root = document.RootElement.Clone();
            Outputs["out"] = root;
            Outputs["Count"] = root.ValueKind == JsonValueKind.Array ? root.GetArrayLength() : 1;
        }
        catch (JsonException ex)
        {
            Outputs["Error"] = ex.Message;
        }
    }
}
