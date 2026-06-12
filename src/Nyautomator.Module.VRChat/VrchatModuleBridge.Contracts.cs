using Nyautomator;

namespace Nyautomator.Modules.VRChat;

public sealed partial class VRChatModuleBridge
{
    /// <summary>
    /// Request body used to start or retry a VRChat username/password login.
    /// </summary>
    public sealed class VRChatLoginRequest
    {
        /// <summary>
        /// Gets the VRChat username or email submitted for login.
        /// </summary>
        public string? Username { get; init; }

        /// <summary>
        /// Gets the VRChat password submitted for login.
        /// </summary>
        public string? Password { get; init; }
    }

    /// <summary>
    /// Request body used for VRChat verification endpoints that accept a single code or token.
    /// </summary>
    public sealed class VRChatCodeRequest
    {
        /// <summary>
        /// Gets the verification code, token, or link submitted to the auth service.
        /// </summary>
        public string? Code { get; init; }
    }

    /// <summary>
    /// Request body used to import VRChat session cookies.
    /// </summary>
    public sealed class VRChatSessionCookieRequest
    {
        /// <summary>
        /// Gets the required VRChat authentication cookie value.
        /// </summary>
        public string? AuthCookie { get; init; }

        /// <summary>
        /// Gets the optional VRChat two-factor cookie value.
        /// </summary>
        public string? TwoFactorCookie { get; init; }
    }

    /// <summary>
    /// Request body used to send a raw OSC avatar parameter update.
    /// </summary>
    public sealed class OscSendRequest
    {
        /// <summary>
        /// Gets the VRChat OSC parameter name to update.
        /// </summary>
        public string? ParameterName { get; init; }

        /// <summary>
        /// Gets the raw value text parsed as bool, integer, or float.
        /// </summary>
        public string? Value { get; init; }
    }

    /// <summary>
    /// Request body used by module endpoints that toggle a feature on or off.
    /// </summary>
    public class ToggleRequest
    {
        /// <summary>
        /// Gets the desired enabled state; missing values are treated as disabled by callers.
        /// </summary>
        public bool? Enabled { get; init; }
    }

    /// <summary>
    /// Request body used to configure OSC passthrough state and ports.
    /// </summary>
    public sealed class OscPassthroughRequest : ToggleRequest
    {
        /// <summary>
        /// Gets the optional external OSC input port override.
        /// </summary>
        public int? InputPort { get; init; }

        /// <summary>
        /// Gets the optional external OSC output port override.
        /// </summary>
        public int? OutputPort { get; init; }
    }

    /// <summary>
    /// Request body used to create a new VRChat Dolly track.
    /// </summary>
    public sealed class CreateTrackRequest
    {
        /// <summary>
        /// Gets the optional display name for the created Dolly track.
        /// </summary>
        public string? Name { get; init; }
    }

    /// <summary>
    /// Request body used to capture the current VRChat camera state into a Dolly track keyframe.
    /// </summary>
    public sealed class CaptureKeyframeRequest
    {
        /// <summary>
        /// Gets the capture mode name parsed as <see cref="VRChatDollyCaptureMode"/>.
        /// </summary>
        public string? Mode { get; init; }

        /// <summary>
        /// Gets the optional target time for time-based capture modes.
        /// </summary>
        public double? TimeSeconds { get; init; }

        /// <summary>
        /// Gets the optional keyframe id for update or replace capture modes.
        /// </summary>
        public string? KeyframeId { get; init; }

        /// <summary>
        /// Gets whether pose data should be included; missing values default to included.
        /// </summary>
        public bool? IncludePose { get; init; }

        /// <summary>
        /// Gets whether camera settings should be included; missing values default to included.
        /// </summary>
        public bool? IncludeSettings { get; init; }
    }

    /// <summary>
    /// Request body used to start playback of a VRChat Dolly track.
    /// </summary>
    public sealed class PlayTrackRequest
    {
        /// <summary>
        /// Gets the playback mode name parsed as <see cref="VRChatDollyRunMode"/>.
        /// </summary>
        public string? RunMode { get; init; }

        /// <summary>
        /// Gets the optional repeat count used by counted playback.
        /// </summary>
        public int? RepeatCount { get; init; }

        /// <summary>
        /// Gets the optional non-negative start delay in seconds.
        /// </summary>
        public double? StartDelaySeconds { get; init; }

        /// <summary>
        /// Gets the optional playback frame rate.
        /// </summary>
        public int? FrameRate { get; init; }

        /// <summary>
        /// Gets the optional group token used to stop related playbacks.
        /// </summary>
        public string? StopGroup { get; init; }

        /// <summary>
        /// Gets the settings application mode parsed as <see cref="VRChatDollySettingsApplyMode"/>.
        /// </summary>
        public string? SettingsApplyMode { get; init; }
    }

    /// <summary>
    /// Request body used to stop VRChat Dolly playback by group.
    /// </summary>
    public sealed class StopTrackRequest
    {
        /// <summary>
        /// Gets the optional stop group used to filter playback stop requests.
        /// </summary>
        public string? StopGroup { get; init; }
    }

    /// <summary>
    /// Module-facing VRChat status DTO returned by authentication endpoints.
    /// </summary>
    public sealed class VRChatStatusDto
    {
        /// <summary>
        /// Gets whether a stored session is available.
        /// </summary>
        public bool HasStoredSession { get; init; }

        /// <summary>
        /// Gets whether the current VRChat session is connected.
        /// </summary>
        public bool IsConnected { get; init; }

        /// <summary>
        /// Gets whether the current login flow is waiting for two-factor verification.
        /// </summary>
        public bool RequiresTwoFactor { get; init; }

        /// <summary>
        /// Gets whether the current login flow is waiting for email code verification.
        /// </summary>
        public bool RequiresEmailCode { get; init; }

        /// <summary>
        /// Gets whether the current login flow is waiting for login-place verification.
        /// </summary>
        public bool RequiresLoginPlaceVerification { get; init; }

        /// <summary>
        /// Gets the two-factor methods offered by VRChat for the current login flow.
        /// </summary>
        public string[] TwoFactorMethods { get; init; } = [];

        /// <summary>
        /// Gets the two-factor methods already completed for the current login flow.
        /// </summary>
        public string[] CompletedTwoFactorMethods { get; init; } = [];

        /// <summary>
        /// Gets the display name of the authenticated VRChat account.
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Gets the user id of the authenticated VRChat account.
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// Gets the account login name of the authenticated VRChat account.
        /// </summary>
        public string? AccountLogin { get; init; }

        /// <summary>
        /// Gets a masked or partial email hint associated with the account.
        /// </summary>
        public string? EmailHint { get; init; }

        /// <summary>
        /// Gets a masked or partial email hint for a pending verification challenge.
        /// </summary>
        public string? PendingEmailHint { get; init; }

        /// <summary>
        /// Gets the UTC time when the account session was last verified.
        /// </summary>
        public DateTime? LastVerifiedUtc { get; init; }

        /// <summary>
        /// Gets the UTC time when the status was last updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; init; }

        /// <summary>
        /// Gets the last authentication error reported by the integration.
        /// </summary>
        public string? LastError { get; init; }

        /// <summary>
        /// Gets whether the integration is configured to reconnect automatically.
        /// </summary>
        public bool AutoReconnect { get; init; }
    }
}
