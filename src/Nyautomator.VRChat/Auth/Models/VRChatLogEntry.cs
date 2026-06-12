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
/// Diagnostic log entry emitted by the VRChat authentication service.
/// </summary>
/// <param name="TimestampUtc">UTC time when the log entry was created.</param>
/// <param name="Level">Log severity label, such as Information, Warning, or Error.</param>
/// <param name="EventName">Stable event name describing the auth pipeline step.</param>
/// <param name="Message">Human-readable log message.</param>
/// <param name="Detail">Optional diagnostic detail with sensitive data redacted by callers where needed.</param>
public sealed record VRChatLogEntry(DateTime TimestampUtc, string Level, string EventName, string Message, string? Detail = null);
