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
/// Result returned by high-level VRChat authentication operations.
/// </summary>
public sealed class VRChatOperationResult
{
    /// <summary>
    /// Gets whether the operation completed successfully from Nyautomator's perspective.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message when the operation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets an optional user-facing success or pending-step message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets the authentication status captured after the operation.
    /// </summary>
    public VRChatStatus Status { get; init; } = new();

    /// <summary>
    /// Creates a successful operation result with the supplied status and optional message.
    /// </summary>
    /// <param name="status">Authentication status to include in the result.</param>
    /// <param name="message">Optional user-facing message.</param>
    /// <returns>A successful operation result.</returns>
    public static VRChatOperationResult CreateSuccess(VRChatStatus status, string? message = null)
        => new() { Success = true, Message = message, Status = status };

    /// <summary>
    /// Creates a failed operation result with the supplied error and status.
    /// </summary>
    /// <param name="error">Failure message to expose to callers.</param>
    /// <param name="status">Authentication status to include in the result.</param>
    /// <returns>A failed operation result.</returns>
    public static VRChatOperationResult CreateFailure(string error, VRChatStatus status)
        => new() { Success = false, Error = error, Status = status };
}
