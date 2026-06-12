using System.Text;
using System.Text.Json;
using Nyautomator;
using Nyautomator.Module.Abstractions;

namespace Nyautomator.Modules.VRChat;

public sealed partial class VRChatModuleBridge
{
    /// <summary>
    /// Routes Dolly module API paths for track management, capture, playback, stop, status, and events.
    /// </summary>
    /// <param name="request">Module API request targeting a Dolly subpath.</param>
    /// <param name="path">Normalized full path beginning with the Dolly segment.</param>
    /// <param name="cancellationToken">Token that cancels request handling or runtime operations.</param>
    /// <returns>A module API response for the requested Dolly operation.</returns>
    private async Task<ModuleApiResponse> HandleDollyAsync(ModuleApiRequest request, string path, CancellationToken cancellationToken)
    {
        ConfigureDollyRuntime();
        var segments = SplitPath(path);
        if (segments.Length == 1 && string.Equals(segments[0], "dolly", StringComparison.OrdinalIgnoreCase))
            return ModuleApiResponse.Json(new { success = true, status = VRChatDollyRuntime.GetStatus() });

        if (segments.Length < 2)
            return NotFound($"VRChat Dolly module API path '/{path}' is not available.");

        if (string.Equals(segments[1], "status", StringComparison.OrdinalIgnoreCase) && IsGet(request))
            return ModuleApiResponse.Json(new { success = true, status = VRChatDollyRuntime.GetStatus() });

        if (string.Equals(segments[1], "events", StringComparison.OrdinalIgnoreCase) && IsGet(request))
            return BuildDollyEventsResponse();

        if (string.Equals(segments[1], "stop", StringComparison.OrdinalIgnoreCase) && IsPost(request))
        {
            var stop = await ReadJsonAsync<StopTrackRequest>(request, cancellationToken).ConfigureAwait(false);
            var result = await VRChatDollyRuntime.StopTrackAsync(null, stop?.StopGroup, cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(result);
        }

        if (!string.Equals(segments[1], "tracks", StringComparison.OrdinalIgnoreCase))
            return NotFound($"VRChat Dolly module API path '/{path}' is not available.");

        if (segments.Length == 2 && IsGet(request))
        {
            var tracks = await VRChatDollyRuntime.ListTracksAsync(cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(new { success = true, tracks });
        }

        if (segments.Length == 2 && IsPost(request))
        {
            var create = await ReadJsonAsync<CreateTrackRequest>(request, cancellationToken).ConfigureAwait(false);
            var track = await VRChatDollyRuntime.CreateTrackAsync(create?.Name, cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(new { success = true, track });
        }

        if (segments.Length < 3)
            return NotFound($"VRChat Dolly module API path '/{path}' is not available.");

        var trackId = segments[2];
        if (segments.Length == 3 && IsGet(request))
        {
            var track = await VRChatDollyRuntime.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
            return track is null
                ? NotFound("Track not found.")
                : ModuleApiResponse.Json(new { success = true, track });
        }

        if (segments.Length == 3 && string.Equals(request.Method, "PUT", StringComparison.OrdinalIgnoreCase))
        {
            var track = await ReadJsonAsync<VRChatDollyTrack>(request, cancellationToken).ConfigureAwait(false);
            if (track is null)
                return Error("Track body is required.");

            track.Id = trackId;
            var saved = await VRChatDollyRuntime.SaveTrackAsync(track, cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(new { success = true, track = saved });
        }

        if (segments.Length == 3 && string.Equals(request.Method, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            var deleted = await VRChatDollyRuntime.DeleteTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
            return deleted ? ModuleApiResponse.Json(new { success = true }) : NotFound("Track not found.");
        }

        if (segments.Length == 4 && string.Equals(segments[3], "duplicate", StringComparison.OrdinalIgnoreCase) && IsPost(request))
        {
            var copy = await VRChatDollyRuntime.DuplicateTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
            return copy is null
                ? NotFound("Track not found.")
                : ModuleApiResponse.Json(new { success = true, track = copy });
        }

        if (segments.Length == 4 && string.Equals(segments[3], "activate", StringComparison.OrdinalIgnoreCase) && IsPost(request))
        {
            var result = await VRChatDollyRuntime.SetActiveTrackAsync(trackId, cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(result, result.Success ? 200 : 400);
        }

        if (segments.Length == 5
            && string.Equals(segments[3], "keyframes", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[4], "capture", StringComparison.OrdinalIgnoreCase)
            && IsPost(request))
        {
            var capture = await ReadJsonAsync<CaptureKeyframeRequest>(request, cancellationToken).ConfigureAwait(false);
            var result = await VRChatDollyRuntime.CaptureKeyframeAsync(
                new VRChatDollyCaptureRequest(
                    trackId,
                    ParseEnum(capture?.Mode, VRChatDollyCaptureMode.Append),
                    capture?.TimeSeconds,
                    capture?.KeyframeId,
                    capture?.IncludePose ?? true,
                    capture?.IncludeSettings ?? true),
                cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(result, result.Success ? 200 : 400);
        }

        if (segments.Length == 4 && string.Equals(segments[3], "play", StringComparison.OrdinalIgnoreCase) && IsPost(request))
        {
            var play = await ReadJsonAsync<PlayTrackRequest>(request, cancellationToken).ConfigureAwait(false);
            var result = await VRChatDollyRuntime.PlayTrackAsync(
                new VRChatDollyPlaybackRequest(
                    trackId,
                    ParseEnum(play?.RunMode, VRChatDollyRunMode.Once),
                    play?.RepeatCount ?? 1,
                    TimeSpan.FromSeconds(Math.Max(0d, play?.StartDelaySeconds ?? 0d)),
                    play?.FrameRate ?? 60,
                    play?.StopGroup,
                    ParseEnum(play?.SettingsApplyMode, VRChatDollySettingsApplyMode.AtKeyframes)),
                cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(result, result.Success ? 200 : 400);
        }

        if (segments.Length == 4 && string.Equals(segments[3], "stop", StringComparison.OrdinalIgnoreCase) && IsPost(request))
        {
            var stop = await ReadJsonAsync<StopTrackRequest>(request, cancellationToken).ConfigureAwait(false);
            var result = await VRChatDollyRuntime.StopTrackAsync(trackId, stop?.StopGroup, cancellationToken).ConfigureAwait(false);
            return ModuleApiResponse.Json(result);
        }

        return NotFound($"VRChat Dolly module API path '/{path}' is not available.");
    }

    /// <summary>
    /// Builds a server-sent events stream that publishes Dolly runtime events and periodic status snapshots.
    /// </summary>
    /// <returns>A streaming module API response using the text/event-stream content type.</returns>
    private static ModuleApiResponse BuildDollyEventsResponse()
    {
        var headers = new Dictionary<string, string>
        {
            ["Cache-Control"] = "no-cache",
            ["Connection"] = "keep-alive"
        };

        return ModuleApiResponse.Stream("text/event-stream", async (stream, cancellationToken) =>
        {
            async Task SendAsync(string eventName, object payload)
            {
                var json = JsonSerializer.Serialize(payload, JsonOptions);
                var data = Encoding.UTF8.GetBytes($"event: {eventName}\ndata: {json}\n\n");
                await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            async void Handler(VRChatDollyEvent evt)
            {
                try
                {
                    await SendAsync("dolly", evt).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            VRChatDollyRuntime.EventEmitted += Handler;
            try
            {
                await SendAsync("status", VRChatDollyRuntime.GetStatus()).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                    await SendAsync("status", VRChatDollyRuntime.GetStatus()).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                VRChatDollyRuntime.EventEmitted -= Handler;
            }
        }, headers: headers);
    }

    /// <summary>
    /// Applies current module configuration to the Dolly runtime, falling back to defaults on configuration errors.
    /// </summary>
    private void ConfigureDollyRuntime()
    {
        try
        {
            var options = GetOptions();
            var dataDirectory = GetModuleDataDirectory();
            var defaultTrackDirectory = string.IsNullOrWhiteSpace(dataDirectory)
                ? null
                : Path.Combine(dataDirectory, "dolly", "tracks");
            VRChatDollyRuntime.Configure(options.Dolly, defaultTrackDirectory);
        }
        catch
        {
            VRChatDollyRuntime.Configure(null, null);
        }
    }

    /// <summary>
    /// Parses an enum name ignoring case and returns a fallback when the value is blank or invalid.
    /// </summary>
    /// <typeparam name="TEnum">Enum type to parse.</typeparam>
    /// <param name="value">Raw enum name from a request body.</param>
    /// <param name="fallback">Value to return when parsing fails.</param>
    /// <returns>The parsed enum value or the supplied fallback.</returns>
    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }
}
