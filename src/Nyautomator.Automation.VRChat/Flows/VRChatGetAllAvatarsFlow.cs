using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Flow node that combines uploaded and favorited VRChat avatars into one de-duplicated list.
/// </summary>
public sealed class VRChatGetAllAvatarsFlow : VRChatAvatarListFlowBase
{
    /// <summary>
    /// Gets the automation registry id for the combined avatar-list flow.
    /// </summary>
    public override string Id => "VRChat.Api.GetAllAvatars";

    /// <summary>
    /// Gets the label shown for this flow in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Get All Avatars";

    /// <summary>
    /// Gets the user-facing explanation of the avatar sources this flow combines.
    /// </summary>
    public override string Description => "Lists uploaded and/or favorited VRChat avatars and combines them by avatar id.";

    /// <summary>
    /// Gets or sets whether avatars uploaded by the current account are included.
    /// </summary>
    public bool IncludeUploaded { get; set; } = true;

    /// <summary>
    /// Gets or sets whether avatars favorited by the current account are included.
    /// </summary>
    public bool IncludeFavorited { get; set; } = true;

    /// <summary>
    /// Fetches the selected avatar sources and de-duplicates them by VRChat avatar id.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token supplied by the base flow.</param>
    /// <returns>A merged response, or the first failed response from the selected sources.</returns>
    protected override async Task<AuthenticatedIntegrationResponse> FetchAsync(CancellationToken cancellationToken)
    {
        var responses = new List<AuthenticatedIntegrationResponse>();
        if (IncludeUploaded)
            responses.Add(await FetchPagedAsync(
                "/avatars",
                ("user", "me"),
                ("releaseStatus", "all"),
                ("sort", "updated"),
                ("order", "descending")).ConfigureAwait(false));
        if (IncludeFavorited)
            responses.Add(await FetchPagedAsync("/avatars/favorites").ConfigureAwait(false));

        var failed = responses.FirstOrDefault(static response => !response.Success || !response.IsSuccess);
        if (failed is not null && (!failed.Success || !failed.IsSuccess))
            return failed;

        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var response in responses)
        {
            if (string.IsNullOrWhiteSpace(response.Body))
                continue;

            using var document = JsonDocument.Parse(response.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var idElement) || idElement.ValueKind != JsonValueKind.String)
                    continue;

                var id = idElement.GetString();
                if (!string.IsNullOrWhiteSpace(id))
                    merged[id] = item.Clone();
            }
        }

        var body = "[" + string.Join(",", merged.Values.Select(static item => item.GetRawText())) + "]";
        return new AuthenticatedIntegrationResponse(true, true, 200, body, string.Empty, null);
    }
}
