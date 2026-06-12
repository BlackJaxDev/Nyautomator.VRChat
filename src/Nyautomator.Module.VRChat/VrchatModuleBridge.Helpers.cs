using System.Text.Json;
using Nyautomator.Module.Abstractions;

namespace Nyautomator.Modules.VRChat;

public sealed partial class VRChatModuleBridge
{
    /// <summary>
    /// Creates a cancellation token linked to the request and capped by the module default timeout.
    /// </summary>
    /// <param name="requestAborted">Request cancellation token supplied by the host.</param>
    /// <returns>A linked cancellation token that cancels after the default timeout.</returns>
    private static CancellationToken CreateRequestToken(CancellationToken requestAborted)
    {
        var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        linked.CancelAfter(DefaultTimeout);
        return linked.Token;
    }

    /// <summary>
    /// Deserializes a module request body as JSON, rewinding seekable streams before reading.
    /// </summary>
    /// <typeparam name="T">Type to deserialize from the request body.</typeparam>
    /// <param name="request">Module API request containing the body stream.</param>
    /// <param name="cancellationToken">Token that cancels deserialization.</param>
    /// <returns>The deserialized body, or <see langword="default"/> when parsing fails.</returns>
    private static async Task<T?> ReadJsonAsync<T>(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.Body.CanSeek)
                request.Body.Position = 0;

            return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// Determines whether the module request uses the GET method.
    /// </summary>
    /// <param name="request">Module API request to inspect.</param>
    /// <returns><see langword="true"/> when the request method is GET.</returns>
    private static bool IsGet(ModuleApiRequest request)
        => string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the module request uses the POST method.
    /// </summary>
    /// <param name="request">Module API request to inspect.</param>
    /// <returns><see langword="true"/> when the request method is POST.</returns>
    private static bool IsPost(ModuleApiRequest request)
        => string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Trims whitespace and leading or trailing slashes from a module API path.
    /// </summary>
    /// <param name="path">Raw module API path.</param>
    /// <returns>The normalized path without surrounding slashes.</returns>
    private static string NormalizePath(string? path)
        => (path ?? string.Empty).Trim().TrimStart('/').TrimEnd('/');

    /// <summary>
    /// Splits a normalized path into URL-decoded segments.
    /// </summary>
    /// <param name="path">Normalized module API path.</param>
    /// <returns>Decoded path segments with empty entries removed.</returns>
    private static string[] SplitPath(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

    /// <summary>
    /// Creates a JSON error response with both message and error fields.
    /// </summary>
    /// <param name="message">Error message to return.</param>
    /// <param name="statusCode">HTTP-style status code for the response.</param>
    /// <returns>A JSON module API error response.</returns>
    private static ModuleApiResponse Error(string message, int statusCode = 400)
        => ModuleApiResponse.Json(new { success = false, message, error = message }, statusCode);

    /// <summary>
    /// Creates a 404 JSON error response.
    /// </summary>
    /// <param name="message">Not-found message to return.</param>
    /// <returns>A JSON module API not-found response.</returns>
    private static ModuleApiResponse NotFound(string message)
        => Error(message, 404);
}
