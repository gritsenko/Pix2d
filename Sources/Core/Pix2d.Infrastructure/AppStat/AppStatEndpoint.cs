#nullable enable
using System;
using System.Linq;
using System.Reflection;

namespace Pix2d;

/// <summary>
/// Resolves the AppStat analytics endpoint from the Sentry DSN that heads bake into their assembly as
/// an <see cref="AssemblyMetadataAttribute"/> ("SentryDsn"). The stats backend lives on the same host
/// as the (self-hosted) Sentry server, so we reuse the DSN's scheme+host and swap in the track path.
/// No DSN → analytics stays disabled, which keeps local/dev builds quiet.
/// </summary>
public static class AppStatEndpoint
{
    private const string TrackPath = "/api/track";

    /// <summary>Reads the "SentryDsn" assembly metadata baked into <paramref name="assembly"/>, if any.</summary>
    public static string? ReadDsn(Assembly assembly) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, "SentryDsn", StringComparison.Ordinal))?.Value;

    /// <summary>
    /// Derives the AppStat track endpoint from a Sentry DSN by reusing its scheme + host (and explicit
    /// port, if any): e.g. <c>https://key@stats.pix2d.com/2</c> → <c>https://stats.pix2d.com/api/track</c>.
    /// Returns false for a null/blank/malformed DSN.
    /// </summary>
    public static bool TryGetTrackUrl(string? dsn, out string trackUrl)
    {
        trackUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(dsn))
            return false;

        try
        {
            var uri = new Uri(dsn);
            var builder = new UriBuilder(uri.Scheme, uri.Host) { Path = TrackPath };
            if (!uri.IsDefaultPort)
                builder.Port = uri.Port;

            trackUrl = builder.Uri.ToString();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
