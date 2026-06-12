using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VRChat.API.Api;
using VRChat.API.Client;
using VRChat.API.Model;

namespace Nyautomator;

/// <summary>
/// Snapshot of the current VRChat authentication session and pending verification state.
/// </summary>
public sealed class VRChatStatus
{
    /// <summary>
    /// Gets whether persisted authentication metadata exists for VRChat.
    /// </summary>
    public bool HasStoredSession { get; init; }

    /// <summary>
    /// Gets whether the service currently has an authenticated VRChat user and no pending verification.
    /// </summary>
    public bool IsConnected { get; init; }

    /// <summary>
    /// Gets whether any two-factor or email verification step is still pending.
    /// </summary>
    public bool RequiresTwoFactor { get; init; }

    /// <summary>
    /// Gets whether the pending verification step is an email one-time code.
    /// </summary>
    public bool RequiresEmailCode { get; init; }

    /// <summary>
    /// Gets whether a legacy login-place approval state is pending.
    /// </summary>
    public bool RequiresLoginPlaceVerification { get; init; }

    /// <summary>
    /// Gets the pending two-factor method names reported by VRChat.
    /// </summary>
    public IReadOnlyList<string> TwoFactorMethods { get; init; } = [];

    /// <summary>
    /// Gets two-factor methods already completed during the current login flow.
    /// </summary>
    public IReadOnlyList<string> CompletedTwoFactorMethods { get; init; } = [];

    /// <summary>
    /// Gets the display name of the authenticated VRChat account when known.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Gets the VRChat user id of the authenticated account when known.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Gets the login name or email used to start the current account session.
    /// </summary>
    public string? AccountLogin { get; init; }

    /// <summary>
    /// Gets a masked account email hint reported by VRChat when available.
    /// </summary>
    public string? EmailHint { get; init; }

    /// <summary>
    /// Gets a masked email hint for a pending verification challenge when available.
    /// </summary>
    public string? PendingEmailHint { get; init; }

    /// <summary>
    /// Gets the UTC time at which this session was last fully verified.
    /// </summary>
    public DateTime? LastVerifiedUtc { get; init; }

    /// <summary>
    /// Gets the UTC time at which persisted session metadata was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; init; }

    /// <summary>
    /// Gets the most recent authentication or session error.
    /// </summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Gets whether startup should attempt to restore a stored session automatically.
    /// </summary>
    public bool AutoReconnect { get; init; }
}
