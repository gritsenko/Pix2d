#nullable enable
using System.Collections.Generic;
using AppStat.Client;

namespace Pix2d.Logging;

/// <summary>
/// Forwards analytics / conversion events to the AppStat backend (stats.pix2d.com). This target is
/// <see cref="EventsOnly"/>, so the <see cref="Logger"/> only delivers entries flagged as events
/// (i.e. produced by <see cref="Logger.LogEventWithParams"/>) — crashes and diagnostic logs stay out
/// of the analytics stream (those go through the crash telemetry pipeline / local log file). The
/// wrapped <see cref="AppStatTrackingClient"/> batches and flushes on its own timer, so
/// <see cref="OnLogged"/> is non-blocking.
/// </summary>
public sealed class AppStatLoggerTarget : ILoggerTarget
{
    private readonly AppStatTrackingClient _client;

    public bool EventsOnly => true;

    public AppStatLoggerTarget(string endpointUrl, string release, string? userId = null, string? os = null)
    {
        _client = new AppStatTrackingClient(endpointUrl, release, userId, os);
    }

    public void OnLogged(LogEntry logEntry)
    {
        try
        {
            // Defensive: crashes/exceptions belong to the crash pipeline, never to analytics.
            if (logEntry.Exception != null)
                return;

            _client.Track(logEntry.Message, BuildProperties(logEntry));
        }
        catch
        {
            // A logging target must never throw.
        }
    }

    /// <summary>Best-effort flush of anything still queued (e.g. on app shutdown).</summary>
    public void Flush() => _ = _client.FlushAsync();

    /// <summary>
    /// Sends a session-stats ping (<c>"@session"</c>, an infrastructure event the server intercepts
    /// and never lists among product events). Counters are cumulative-since-process-start; the server
    /// upserts by session id with a max-merge, so out-of-order / duplicate pings are harmless.
    /// Bypasses <see cref="Logger"/> so these pings don't spam the log file or other targets.
    /// </summary>
    public void TrackSessionStats(long activeSeconds, long wallSeconds, System.DateTime startedUtc, string? platform)
    {
        try
        {
            _client.Track("@session", new Dictionary<string, object>
            {
                ["activeSeconds"] = activeSeconds,
                ["wallSeconds"] = wallSeconds,
                ["startedUtc"] = startedUtc,
                ["platform"] = platform ?? string.Empty,
            });
        }
        catch
        {
            // A logging target must never throw.
        }
    }

    private static IReadOnlyDictionary<string, object>? BuildProperties(LogEntry e)
    {
        var hasParams = e.ExtraParams is { Count: > 0 };
        var hasMetrics = e.Metrics is { Count: > 0 };
        if (!hasParams && !hasMetrics)
            return null;

        var props = new Dictionary<string, object>();
        if (hasParams)
            foreach (var (key, value) in e.ExtraParams!)
                props[key] = value;
        if (hasMetrics)
            foreach (var (key, value) in e.Metrics!)
                props[key] = value;

        return props;
    }
}
