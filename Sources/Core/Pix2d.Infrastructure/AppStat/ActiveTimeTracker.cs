// ---------------------------------------------------------------------------
// ActiveTimeTracker — measures *active* usage time, not process wall-clock.
//
// The dashboard's old "session duration" is Sentry release-health wall-clock
// (process alive, including minimized / backgrounded), which inflates the
// average to hours. This accumulator instead counts only the time the app is
// BOTH foreground AND has had user input within an idle timeout — i.e. real
// editing time.
//
// Pure BCL, no Avalonia dependency (AOT/trim/WASM-safe). Avalonia signals are
// fed in by ActiveSessionLifecycleHost; the report is sent by
// SessionStatsReporter over the existing /api/track pipeline.
//
// MODEL
// -----
// A monotonic clock (Environment.TickCount64) is the ONLY time source for
// accumulation — wall-clock deltas would be corrupted by system-clock changes
// and by machine sleep. An interval of active time is "open" while the app is
// foreground and the idle lease (last input + IdleTimeout) has not expired.
// Every public call *settles* first: if an interval was open, the elapsed
// active time — clamped to the idle lease — is folded into the running total
// and the interval closed; then a fresh interval re-opens if we are still
// active. This makes idle expiry retroactively correct with no internal timer.
// ---------------------------------------------------------------------------

using System;

namespace Pix2d.Infrastructure.AppStat;

public sealed class ActiveTimeTracker
{
    private readonly object _lock = new();
    private readonly long _idleTimeoutMs;
    private readonly Func<long> _clockMs;
    private readonly long _createdAtMs;

    private long _accumulatedMs;      // settled active time
    private bool _isForeground;       // last foreground signal (app launches foreground)
    private long _lastInputMs;        // monotonic timestamp of the last input signal
    private long? _openIntervalStart; // start of the currently-open active interval, if any

    /// <summary>Captured once, so the server can group/window sessions by their start.</summary>
    public DateTime StartedUtc { get; }

    /// <param name="idleTimeout">
    /// How long after the last input the user is still considered active. Default 5 min —
    /// drawing produces near-continuous input, so this tolerates "thinking with the stylus
    /// down" without counting a coffee break.
    /// </param>
    /// <param name="clockMs">Monotonic millisecond clock; injectable for tests.</param>
    public ActiveTimeTracker(TimeSpan? idleTimeout = null, Func<long>? clockMs = null)
    {
        _idleTimeoutMs = (long)(idleTimeout ?? TimeSpan.FromMinutes(5)).TotalMilliseconds;
        _clockMs = clockMs ?? (() => Environment.TickCount64);

        StartedUtc = DateTime.UtcNow;
        _createdAtMs = _clockMs();
        _lastInputMs = _createdAtMs;
        _isForeground = true;                 // the app is foreground the moment it launches
        _openIntervalStart = _createdAtMs;    // ...and the launch itself counts as activity
    }

    /// <summary>Foreground gained/lost — window (de)activation or app (un)backgrounding.</summary>
    public void NotifyForeground(bool isForeground)
    {
        lock (_lock)
        {
            Settle(_clockMs());
            _isForeground = isForeground;
            if (isForeground)
                _lastInputMs = _clockMs(); // treat returning to the app as fresh activity
            ReopenIfActive(_clockMs());
        }
    }

    /// <summary>Any user input (pointer / key / wheel). Cheap: a lock + a couple of longs.</summary>
    public void NotifyInput()
    {
        lock (_lock)
        {
            var now = _clockMs();
            Settle(now);
            _lastInputMs = now;
            ReopenIfActive(now);
        }
    }

    /// <summary>Settle the current interval and return cumulative active + wall totals.</summary>
    public Snapshot GetSnapshot()
    {
        lock (_lock)
        {
            var now = _clockMs();
            Settle(now);
            ReopenIfActive(now); // keep counting after a snapshot (reporter reads periodically)

            var wallMs = now - _createdAtMs;
            return new Snapshot(_accumulatedMs / 1000, wallMs / 1000, StartedUtc);
        }
    }

    // Fold any open interval's elapsed active time into the total, then close it. Active time
    // ends at the idle lease (lastInput + timeout) if that falls before now, so an interval
    // that went idle is credited only up to the timeout, not to the settling moment.
    private void Settle(long now)
    {
        if (_openIntervalStart is not { } start)
            return;

        var activeUntil = Math.Min(now, _lastInputMs + _idleTimeoutMs);
        var delta = activeUntil - start;
        if (delta > 0)
            _accumulatedMs += delta;

        _openIntervalStart = null;
    }

    // Re-open an interval only while genuinely active: foreground AND within the idle lease.
    private void ReopenIfActive(long now)
    {
        if (_isForeground && now < _lastInputMs + _idleTimeoutMs)
            _openIntervalStart = now;
    }

    public readonly record struct Snapshot(long ActiveSeconds, long WallSeconds, DateTime StartedUtc);
}
