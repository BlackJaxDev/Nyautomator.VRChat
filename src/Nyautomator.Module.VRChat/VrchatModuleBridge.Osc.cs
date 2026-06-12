using System.Text;
using System.Text.Json;
using Nyautomator;
using Nyautomator.Module.Abstractions;

namespace Nyautomator.Modules.VRChat;

public sealed partial class VRChatModuleBridge
{
    /// <summary>
    /// Routes OSC module API paths for status, parameter metadata, event streaming, sending, and toggles.
    /// </summary>
    /// <param name="request">Module API request targeting an OSC subpath.</param>
    /// <param name="path">Normalized full path beginning with the OSC segment.</param>
    /// <param name="cancellationToken">Token that cancels request handling.</param>
    /// <returns>A module API response for the requested OSC operation.</returns>
    private async Task<ModuleApiResponse> HandleOscAsync(ModuleApiRequest request, string path, CancellationToken cancellationToken)
    {
        var subPath = path.Length <= "osc/".Length ? string.Empty : path["osc/".Length..];
        return subPath.ToLowerInvariant() switch
        {
            "status" when IsGet(request) => ModuleApiResponse.Json(BuildOscStatus()),
            "params" when IsGet(request) => ModuleApiResponse.Json(BuildOscParams()),
            "events" when IsGet(request) => BuildOscEventsResponse(),
            "send" when IsPost(request) => await SendOscAsync(request, cancellationToken).ConfigureAwait(false),
            "sending" when IsPost(request) => await ToggleOscSendingAsync(request, cancellationToken).ConfigureAwait(false),
            "listening" when IsPost(request) => await ToggleOscListeningAsync(request, cancellationToken).ConfigureAwait(false),
            "passthrough" when IsPost(request) => await ToggleOscPassthroughAsync(request, cancellationToken).ConfigureAwait(false),
            _ => NotFound($"VRChat OSC module API path '/{path}' is not available.")
        };
    }

    /// <summary>
    /// Reads a parameter name and value from the request body and sends the value to VRChat over OSC.
    /// </summary>
    /// <param name="request">Module API request whose body contains OSC send fields.</param>
    /// <param name="cancellationToken">Token that cancels JSON body reading.</param>
    /// <returns>A JSON response indicating whether the value was parsed and sent.</returns>
    private async Task<ModuleApiResponse> SendOscAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<OscSendRequest>(request, cancellationToken).ConfigureAwait(false);
        if (body?.ParameterName is null || body.Value is null)
            return Error("ParameterName and Value are required.");

        var ok = TrySetOscParameter(body.ParameterName, body.Value);
        return ModuleApiResponse.Json(new { success = ok });
    }

    /// <summary>
    /// Starts or stops OSC sending using the configured sender port.
    /// </summary>
    /// <param name="request">Module API request whose body contains the desired enabled state.</param>
    /// <param name="cancellationToken">Token that cancels JSON body reading.</param>
    /// <returns>A JSON response containing the resulting sending state.</returns>
    private async Task<ModuleApiResponse> ToggleOscSendingAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<ToggleRequest>(request, cancellationToken).ConfigureAwait(false);
        var osc = GetOptions().Osc;
        if (body?.Enabled == true)
            VRChatHelper.OSC.StartSending(osc.SenderPort);
        else
            VRChatHelper.OSC.StopSending();

        return ModuleApiResponse.Json(new { sending = VRChatHelper.OSC.Sending });
    }

    /// <summary>
    /// Starts or stops OSC listening using the configured listener port.
    /// </summary>
    /// <param name="request">Module API request whose body contains the desired enabled state.</param>
    /// <param name="cancellationToken">Token that cancels JSON body reading.</param>
    /// <returns>A JSON response containing the resulting listening state.</returns>
    private async Task<ModuleApiResponse> ToggleOscListeningAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<ToggleRequest>(request, cancellationToken).ConfigureAwait(false);
        var osc = GetOptions().Osc;
        if (body?.Enabled == true)
        {
            if (!VRChatHelper.OSC.Listening)
                _ = VRChatHelper.OSC.StartListening(osc.ListenerPort);
        }
        else
        {
            VRChatHelper.OSC.StopListening();
        }

        return ModuleApiResponse.Json(new { listening = VRChatHelper.OSC.Listening });
    }

    /// <summary>
    /// Configures OSC passthrough state and ports, only enabling passthrough when VRChat OSC is enabled in configuration.
    /// </summary>
    /// <param name="request">Module API request whose body contains passthrough state and optional ports.</param>
    /// <param name="cancellationToken">Token that cancels JSON body reading.</param>
    /// <returns>A JSON response containing the resulting passthrough state and ports.</returns>
    private async Task<ModuleApiResponse> ToggleOscPassthroughAsync(ModuleApiRequest request, CancellationToken cancellationToken)
    {
        var body = await ReadJsonAsync<OscPassthroughRequest>(request, cancellationToken).ConfigureAwait(false);
        var osc = GetOptions().Osc;
        var enable = (body?.Enabled ?? false) && osc.Enabled;
        var inputPort = body?.InputPort ?? osc.PassthroughInputPort;
        var outputPort = body?.OutputPort ?? osc.PassthroughOutputPort;

        VRChatHelper.OSC.ConfigurePassthrough(enable, inputPort, outputPort);
        return ModuleApiResponse.Json(new
        {
            passthrough = VRChatHelper.OSC.PassthroughEnabled,
            inputPort = VRChatHelper.OSC.ExternalInputPort,
            outputPort = VRChatHelper.OSC.ExternalOutputPort
        });
    }

    /// <summary>
    /// Builds a server-sent events stream that publishes OSC avatar parameter snapshots and keepalives.
    /// </summary>
    /// <returns>A streaming module API response using the text/event-stream content type.</returns>
    private ModuleApiResponse BuildOscEventsResponse()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cache-Control"] = "no-cache, no-transform",
            ["Connection"] = "keep-alive",
            ["X-Accel-Buffering"] = "no"
        };

        return ModuleApiResponse.Stream("text/event-stream", async (stream, cancellationToken) =>
        {
            try
            {
                var osc = GetOptions().Osc;
                if (osc.Enabled && !VRChatHelper.OSC.Listening)
                    _ = VRChatHelper.OSC.StartListening(osc.ListenerPort);
            }
            catch
            {
            }

            string SnapshotJson() => JsonSerializer.Serialize(BuildOscParams(), JsonOptions);

            async Task WriteEventAsync(string json)
            {
                var data = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            var lastSent = SnapshotJson();
            await WriteEventAsync(lastSent).ConfigureAwait(false);

            void Handler()
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var current = SnapshotJson();
                        lastSent = current;
                        await WriteEventAsync(current).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }, CancellationToken.None);
            }

            VRChatHelper.OSC.OnAvatarConfigChanged += Handler;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
                    var current = SnapshotJson();
                    if (!string.Equals(current, lastSent, StringComparison.Ordinal))
                    {
                        lastSent = current;
                        await WriteEventAsync(current).ConfigureAwait(false);
                    }
                    else
                    {
                        var keepAlive = Encoding.UTF8.GetBytes(":\n\n");
                        await stream.WriteAsync(keepAlive, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                VRChatHelper.OSC.OnAvatarConfigChanged -= Handler;
            }
        }, headers: headers);
    }

    /// <summary>
    /// Builds a lightweight OSC status object from the current helper state.
    /// </summary>
    /// <returns>An anonymous response object containing OSC state and loaded avatar metadata.</returns>
    private static object BuildOscStatus()
        => new
        {
            success = true,
            sending = VRChatHelper.OSC.Sending,
            listening = VRChatHelper.OSC.Listening,
            passthrough = VRChatHelper.OSC.PassthroughEnabled,
            inputPort = VRChatHelper.OSC.ExternalInputPort,
            outputPort = VRChatHelper.OSC.ExternalOutputPort,
            configured = VRChatHelper.OSC.IsAvatarConfigured,
            avatarId = VRChatHelper.OSC.AvatarId,
            avatarName = VRChatHelper.OSC.AvatarName
        };

    /// <summary>
    /// Builds OSC avatar parameter metadata, attempting to load the latest avatar config when needed.
    /// </summary>
    /// <returns>An anonymous response object containing avatar metadata and parameter names/types.</returns>
    private static object BuildOscParams()
    {
        if (!VRChatHelper.OSC.IsAvatarConfigured)
        {
            try { VRChatHelper.OSC.TryLoadLatestAvatarConfig(); }
            catch { }
        }

        return new
        {
            configured = VRChatHelper.OSC.IsAvatarConfigured,
            avatarId = VRChatHelper.OSC.AvatarId,
            avatarName = VRChatHelper.OSC.AvatarName,
            parameters = VRChatHelper.OSC.CurrentParameters
                .Select(p => new { name = p.Name, type = p.Type })
                .ToArray()
        };
    }

    /// <summary>
    /// Parses a raw OSC value as bool, integer, or float and sends it to the named parameter.
    /// </summary>
    /// <param name="parameterName">VRChat OSC parameter name to update.</param>
    /// <param name="value">Raw value text to parse.</param>
    /// <returns><see langword="true"/> when a supported primitive value was sent.</returns>
    private static bool TrySetOscParameter(string parameterName, string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            VRChatHelper.OSC.SetParameter(parameterName, boolean);
            return true;
        }

        if (int.TryParse(value, out var integer))
        {
            VRChatHelper.OSC.SetParameter(parameterName, integer);
            return true;
        }

        if (float.TryParse(value, out var floating))
        {
            VRChatHelper.OSC.SetParameter(parameterName, floating);
            return true;
        }

        return false;
    }
}
