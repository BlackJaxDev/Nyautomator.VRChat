using System.Threading;
using System.Threading.Tasks;

namespace NyautomatorUI.Server.Automation;

/// <summary>
/// Flow node that lists avatars favorited by the logged-in VRChat account.
/// </summary>
public sealed class VRChatGetFavoritedAvatarsFlow : VRChatAvatarListFlowBase
{
    /// <summary>
    /// Gets the automation registry id for the favorited-avatar list flow.
    /// </summary>
    public override string Id => "VRChat.Api.GetFavoritedAvatars";

    /// <summary>
    /// Gets the label shown for this flow in automation tooling.
    /// </summary>
    public override string DisplayName => "VRChat: Get Favorited Avatars";

    /// <summary>
    /// Gets the user-facing explanation of the avatar list source.
    /// </summary>
    public override string Description => "Lists avatars favorited by the logged-in VRChat account.";

    /// <summary>
    /// Gets or sets the optional VRChat favorite tag query value.
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Fetches favorited avatars, passing the optional tag filter to the paging helper.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token supplied by the base flow.</param>
    /// <returns>The authenticated integration response for the favorited-avatar list.</returns>
    protected override Task<AuthenticatedIntegrationResponse> FetchAsync(CancellationToken cancellationToken)
        => FetchPagedAsync("/avatars/favorites", ("tag", Tag));
}
