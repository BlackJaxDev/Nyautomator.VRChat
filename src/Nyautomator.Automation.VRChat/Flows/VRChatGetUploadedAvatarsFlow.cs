using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Flow node that lists avatars uploaded by the logged-in VRChat account.
/// </summary>
public sealed class VRChatGetUploadedAvatarsFlow : VRChatAvatarListFlowBase
{
    /// <summary>
    /// Gets the automation registry id for the uploaded-avatar list flow.
    /// </summary>
    public override string Id => "VRChat.Api.GetUploadedAvatars";

    /// <summary>
    /// Gets the label shown for this flow in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Get Uploaded Avatars";

    /// <summary>
    /// Gets the user-facing explanation of the avatar list source.
    /// </summary>
    public override string Description => "Lists avatars uploaded by the logged-in VRChat account.";

    /// <summary>
    /// Fetches the current account's uploaded avatars sorted by most recently updated.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token supplied by the base flow.</param>
    /// <returns>The authenticated integration response for the uploaded-avatar list.</returns>
    protected override Task<AuthenticatedIntegrationResponse> FetchAsync(CancellationToken cancellationToken)
        => FetchPagedAsync(
            "/avatars",
            ("user", "me"),
            ("releaseStatus", "all"),
            ("sort", "updated"),
            ("order", "descending"));
}
