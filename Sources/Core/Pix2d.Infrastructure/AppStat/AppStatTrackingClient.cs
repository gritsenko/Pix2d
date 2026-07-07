// ---------------------------------------------------------------------------
// AppStatTrackingClient — drop-in custom-event tracker for AppStatServer.
//
// Meant to be copied straight into Pix2d. Single file, no dependencies beyond
// the BCL. AOT/trim-safe: JSON is written by hand with Utf8JsonWriter (no
// reflection), so it works under NativeAOT and the Browser/WASM head.
//
// USAGE
// -----
//   // once at startup (keep the instance alive for the app lifetime):
//   var stats = new AppStatTrackingClient(
//       endpointUrl: "https://your-appstatserver/api/track",
//       release:     "1.2.3",
//       userId:      installId);          // your stable anonymous install id
//
//   // anywhere:
//   stats.Track("project_opened");
//   stats.Track("buy_clicked",         new Dictionary<string, object> { ["productId"] = "pro", ["price"] = 4.99 });
//   stats.Track("purchase_cancelled",  new Dictionary<string, object> { ["productId"] = "pro", ["reason"] = "back" });
//
//   // on app exit (flush anything still queued):
//   await stats.DisposeAsync();
//
// Track() is non-blocking: events are queued and flushed in batches (on size or
// on a timer). Property values may be string / bool / int / long / double /
// decimal / DateTime. Server-side limits (256 name, 20 props, 125 key/value)
// are mirrored here so we don't waste bytes on the wire.
// ---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AppStat.Client;

public sealed class AppStatTrackingClient : IAsyncDisposable, IDisposable
{
    // Mirror of the server's AppCenter-style limits.
    private const int MaxNameLength = 256;
    private const int MaxProperties = 20;
    private const int MaxKeyLength = 125;
    private const int MaxValueLength = 125;

    private readonly Uri _endpoint;
    private readonly string _release;
    private readonly string? _userId;
    private readonly string? _os;

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    private readonly ConcurrentQueue<QueuedEvent> _queue = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly Timer _timer;
    private int _queueCount;

    /// <summary>Session id shared by every event this instance sends; join key with sessions/crashes.</summary>
    public string SessionId { get; }

    /// <summary>Flush once this many events are queued.</summary>
    public int BatchSize { get; init; } = 20;

    /// <summary>Ring-buffer cap: if the app is offline for a long time, oldest events are dropped past this.</summary>
    public int MaxQueue { get; init; } = 500;

    public AppStatTrackingClient(
        string endpointUrl,
        string release,
        string? userId = null,
        string? os = null,
        HttpClient? httpClient = null,
        TimeSpan? flushInterval = null)
    {
        _endpoint = new Uri(endpointUrl);
        _release = release;
        _userId = userId;
        _os = os ?? RuntimeInformation.OSDescription;

        _http = httpClient ?? new HttpClient();
        _ownsHttp = httpClient is null;

        SessionId = Guid.NewGuid().ToString("N");

        var interval = flushInterval ?? TimeSpan.FromSeconds(10);
        _timer = new Timer(_ => _ = FlushAsync(), null, interval, interval);
    }

    /// <summary>Queue a custom event. Non-blocking; safe to call from any thread / the UI thread.</summary>
    public void Track(string name, IReadOnlyDictionary<string, object>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _queue.Enqueue(new QueuedEvent(Truncate(name, MaxNameLength), DateTime.UtcNow, Sanitize(properties)));
        Interlocked.Increment(ref _queueCount);

        // Ring buffer: drop oldest if we've exceeded the cap (e.g. long offline stretch).
        while (Volatile.Read(ref _queueCount) > MaxQueue && _queue.TryDequeue(out _))
            Interlocked.Decrement(ref _queueCount);

        if (Volatile.Read(ref _queueCount) >= BatchSize)
            _ = FlushAsync();
    }

    /// <summary>Send everything currently queued. Called automatically on the timer and on dispose.</summary>
    public async Task FlushAsync()
    {
        // Skip if a flush is already running — it will keep draining the queue.
        if (!await _flushLock.WaitAsync(0).ConfigureAwait(false))
            return;

        try
        {
            while (true)
            {
                var batch = new List<QueuedEvent>(BatchSize);
                while (batch.Count < BatchSize && _queue.TryDequeue(out var e))
                {
                    Interlocked.Decrement(ref _queueCount);
                    batch.Add(e);
                }

                if (batch.Count == 0)
                    return;

                try
                {
                    using var content = new StringContent(SerializeBatch(batch), Encoding.UTF8, "application/json");
                    using var response = await _http.PostAsync(_endpoint, content).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        Requeue(batch);
                        return; // back off; retry on the next interval
                    }
                }
                catch
                {
                    Requeue(batch); // network error — keep events for the next attempt
                    return;
                }
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private void Requeue(List<QueuedEvent> batch)
    {
        foreach (var e in batch)
        {
            _queue.Enqueue(e);
            Interlocked.Increment(ref _queueCount);
        }

        while (Volatile.Read(ref _queueCount) > MaxQueue && _queue.TryDequeue(out _))
            Interlocked.Decrement(ref _queueCount);
    }

    private string SerializeBatch(List<QueuedEvent> batch)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            if (_userId is not null) w.WriteString("userId", _userId);
            w.WriteString("sessionId", SessionId);
            w.WriteString("release", _release);
            if (_os is not null) w.WriteString("os", _os);

            w.WriteStartArray("events");
            foreach (var e in batch)
            {
                w.WriteStartObject();
                w.WriteString("name", e.Name);
                w.WriteString("timestamp", e.Timestamp.ToString("o", CultureInfo.InvariantCulture));

                if (e.Properties is { Count: > 0 })
                {
                    w.WriteStartObject("properties");
                    foreach (var (key, value) in e.Properties)
                        WriteValue(w, key, value);
                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }
            w.WriteEndArray();

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteValue(Utf8JsonWriter w, string key, object value)
    {
        switch (value)
        {
            case string s: w.WriteString(key, s); break;
            case bool b: w.WriteBoolean(key, b); break;
            case int i: w.WriteNumber(key, i); break;
            case long l: w.WriteNumber(key, l); break;
            case double d: w.WriteNumber(key, d); break;
            case float f: w.WriteNumber(key, f); break;
            case decimal m: w.WriteNumber(key, m); break;
            case DateTime dt: w.WriteString(key, dt.ToString("o", CultureInfo.InvariantCulture)); break;
            default: w.WriteString(key, value.ToString() ?? string.Empty); break;
        }
    }

    private static Dictionary<string, object>? Sanitize(IReadOnlyDictionary<string, object>? props)
    {
        if (props is null || props.Count == 0)
            return null;

        var result = new Dictionary<string, object>(Math.Min(props.Count, MaxProperties));
        foreach (var (key, value) in props)
        {
            if (result.Count >= MaxProperties)
                break;
            if (string.IsNullOrEmpty(key) || value is null)
                continue;

            result[Truncate(key, MaxKeyLength)] = value is string s ? Truncate(s, MaxValueLength) : value;
        }

        return result;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().ConfigureAwait(false);
        await FlushAsync().ConfigureAwait(false);
        if (_ownsHttp) _http.Dispose();
        _flushLock.Dispose();
    }

    // Prefer DisposeAsync() where you can await — on the single-threaded WASM head,
    // blocking on FlushAsync() here can deadlock. This sync path is a best-effort fallback.
    public void Dispose()
    {
        _timer.Dispose();
        try { FlushAsync().GetAwaiter().GetResult(); } catch { /* best effort */ }
        if (_ownsHttp) _http.Dispose();
        _flushLock.Dispose();
    }

    private readonly record struct QueuedEvent(string Name, DateTime Timestamp, Dictionary<string, object>? Properties);
}
