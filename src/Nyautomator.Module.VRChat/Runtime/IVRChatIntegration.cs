using Nyautomator;
using Nyautomator.Runtime.Abstractions;

namespace Nyautomator.Modules.VRChat;

/// <summary>
/// Runtime participant contract for the VRChat integration and its authentication services.
/// </summary>
public interface IVRChatIntegration : IRuntimeParticipant
{
    /// <summary>
    /// Gets the authentication service used for VRChat login, session restore, and authenticated API requests.
    /// </summary>
    VRChatAuthService Auth { get; }

    Task ConfigureAsync(VRChatModuleOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to restore a stored VRChat session through the authentication service.
    /// </summary>
    /// <param name="forceRefresh">Whether the restore should force a status refresh after loading stored session data.</param>
    /// <param name="cancellationToken">Token that cancels session restoration.</param>
    /// <returns>The latest VRChat authentication status after the restore attempt.</returns>
    Task<VRChatStatus> RestoreSessionAsync(bool forceRefresh, CancellationToken cancellationToken);

    /// <summary>
    /// Occurs when the VRChat authentication service emits a diagnostic log entry.
    /// </summary>
    event Action<VRChatLogEntry>? LogEmitted;
}
