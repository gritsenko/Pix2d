#nullable enable
using System;
using System.Threading;
using Pix2d.Infrastructure.AppStat;
using Pix2d.Logging;

namespace Pix2d.Services.Telemetry;

/// <summary>
/// Periodically pushes <see cref="ActiveTimeTracker"/> totals to the AppStat backend as
/// <c>"@session"</c> pings over the existing <see cref="AppStatLoggerTarget"/> transport
/// (batching / offline handling / consent gating all come for free).
///
/// The timer fires every <see cref="Interval"/>; a ping is skipped when active time hasn't grown
/// since the last one (an idle / backgrounded app stops pinging — its wall-clock then freezes near
/// the last activity instead of inflating). <see cref="ReportNow"/> with <c>force</c> ignores that
/// and flushes immediately — wire it to shutdown / backgrounding / app-exit.
///
/// Lifetime is owned by the bootstrapper: created in <c>EnableAnalytics</c>, disposed in
/// <c>DisableAnalytics</c>, so a consent withdrawal stops session pings too.
/// </summary>
public sealed class SessionStatsReporter : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly ActiveTimeTracker _tracker;
    private readonly AppStatLoggerTarget _target;
    private readonly string? _platform;
    private readonly Timer _timer;
    private readonly object _lock = new();

    private long _lastReportedActiveSeconds = -1;
    private bool _disposed;

    public SessionStatsReporter(ActiveTimeTracker tracker, AppStatLoggerTarget target, string? platform)
    {
        _tracker = tracker;
        _target = target;
        _platform = platform;
        _timer = new Timer(_ => ReportNow(force: false), null, Interval, Interval);
    }

    /// <summary>
    /// Sends the current totals. When <paramref name="force"/> is false the ping is skipped if
    /// active time hasn't advanced since the last send; when true it always sends and flushes.
    /// </summary>
    public void ReportNow(bool force)
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            var snap = _tracker.GetSnapshot();
            if (!force && snap.ActiveSeconds <= _lastReportedActiveSeconds)
                return;

            _lastReportedActiveSeconds = snap.ActiveSeconds;
            _target.TrackSessionStats(snap.ActiveSeconds, snap.WallSeconds, snap.StartedUtc, _platform);

            if (force)
                _target.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        _timer.Dispose();
    }
}
