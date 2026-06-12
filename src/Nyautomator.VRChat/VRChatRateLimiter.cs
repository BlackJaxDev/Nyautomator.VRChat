using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nyautomator;

/// <summary>
/// Sliding-window rate limiter modeled on VRCX's <c>createRateLimiter</c> guardrail.
/// Tracks recent request timestamps and delays callers when the per-interval cap is reached,
/// so all VRChat API traffic stays within a conservative ceiling.
/// </summary>
internal sealed class VRChatRateLimiter
{
    /// <summary>
    /// Maximum number of reservations allowed within the sliding interval.
    /// </summary>
    private readonly int _limit;

    /// <summary>
    /// Sliding time window used to age out previous reservations.
    /// </summary>
    private readonly TimeSpan _interval;

    /// <summary>
    /// Queue of UTC timestamps for reservations still inside the current sliding window.
    /// </summary>
    private readonly Queue<DateTime> _stamps = new();

    /// <summary>
    /// Serializes access to the timestamp queue while callers reserve request capacity.
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a limiter with a positive request limit and interval.
    /// </summary>
    /// <param name="limitPerInterval">Maximum reservations allowed per interval.</param>
    /// <param name="interval">Sliding interval for reservation timestamps.</param>
    public VRChatRateLimiter(int limitPerInterval, TimeSpan interval)
    {
        _limit = Math.Max(1, limitPerInterval);
        _interval = interval <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : interval;
    }

    /// <summary>
    /// Reserves a slot in the current window, awaiting until capacity is available.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels waiting for capacity.</param>
    /// <returns>A task that completes when the caller has reserved a request slot.</returns>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan delay;
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                while (_stamps.Count > 0 && now - _stamps.Peek() > _interval)
                    _stamps.Dequeue();

                if (_stamps.Count < _limit)
                {
                    _stamps.Enqueue(now);
                    return;
                }

                delay = _interval - (now - _stamps.Peek());
                if (delay < TimeSpan.Zero)
                    delay = TimeSpan.Zero;
            }
            finally
            {
                _gate.Release();
            }

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
