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
/// Raw response returned by an authenticated VRChat cloud API request.
/// </summary>
/// <param name="Success">Whether Nyautomator was able to send the request and receive a response.</param>
/// <param name="IsSuccess">Whether the VRChat HTTP response status code was successful.</param>
/// <param name="StatusCode">HTTP status code returned by VRChat, or 0 when no response was received.</param>
/// <param name="Body">Response body text returned by VRChat.</param>
/// <param name="Headers">Flattened response headers returned by VRChat.</param>
/// <param name="Error">Error text when the request failed or returned a non-success status.</param>
public sealed record VRChatRawApiResponse(
    bool Success,
    bool IsSuccess,
    int StatusCode,
    string Body,
    string Headers,
    string? Error);
