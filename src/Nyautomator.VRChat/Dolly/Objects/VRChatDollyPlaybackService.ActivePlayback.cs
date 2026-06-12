using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nyautomator;

public sealed partial class VRChatDollyPlaybackService
{
    /// <summary>
    /// Mutable state and cancellation ownership for a single active playback session.
    /// </summary>
    /// <remarks>
    /// Creates active playback state for a loaded track and normalized request.
    /// </remarks>
    /// <param name="track">Track being played.</param>
    /// <param name="request">Normalized playback request.</param>
    private sealed class ActivePlayback(VRChatDollyTrack track, VRChatDollyPlaybackRequest request) : IDisposable
    {
        /// <summary>
        /// Gets the unique playback session identifier.
        /// </summary>
        public string PlaybackId { get; } = VRChatDollyKeyframe.CreateId("playback");

        /// <summary>
        /// Gets the track being played.
        /// </summary>
        public VRChatDollyTrack Track { get; } = track;

        /// <summary>
        /// Gets the normalized playback request.
        /// </summary>
        public VRChatDollyPlaybackRequest Request { get; } = request;

        /// <summary>
        /// Gets the cancellation token source used to stop playback.
        /// </summary>
        public CancellationTokenSource Cts { get; } = new();

        /// <summary>
        /// Gets or sets the background task running the playback loop.
        /// </summary>
        public Task? RunTask { get; set; }

        /// <summary>
        /// Gets when the playback session started.
        /// </summary>
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets when the playback session stopped, when known.
        /// </summary>
        public DateTime? StoppedAtUtc { get; private set; }

        /// <summary>
        /// Gets or sets the number of completed playback loops.
        /// </summary>
        public int CompletedLoops { get; set; }

        /// <summary>
        /// Gets or sets the latest track time sent by playback.
        /// </summary>
        public double TrackTimeSeconds { get; set; }

        /// <summary>
        /// Gets the last stop reason or error associated with playback.
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// Cancels playback and records a stop reason.
        /// </summary>
        /// <param name="reason">Reason recorded on the session state.</param>
        public void Stop(string? reason)
        {
            LastError = reason;
            try { Cts.Cancel(); } catch { }
        }

        /// <summary>
        /// Marks playback as stopped and stores an optional failure message.
        /// </summary>
        /// <param name="error">Failure message, or null for normal stop.</param>
        public void MarkStopped(string? error)
        {
            LastError = error;
            StoppedAtUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Builds an immutable snapshot of this active playback state.
        /// </summary>
        /// <returns>The current playback state.</returns>
        public VRChatDollyPlaybackState GetState()
            => new(
                PlaybackId,
                Track.Id,
                Track.Name,
                Request.RunMode,
                Request.RepeatCount,
                CompletedLoops,
                Request.FrameRate,
                Request.StopGroup,
                Request.SettingsApplyMode,
                StartedAtUtc,
                StoppedAtUtc,
                TrackTimeSeconds,
                StoppedAtUtc is null && !Cts.IsCancellationRequested,
                LastError);

        /// <summary>
        /// Cancels and disposes playback cancellation resources.
        /// </summary>
        public void Dispose()
        {
            try { Cts.Cancel(); } catch { }
            Cts.Dispose();
        }
    }
}
