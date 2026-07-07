#nullable enable
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;

namespace Pix2d.Services;

/// <summary>
/// Queries the GitHub "latest release" API and reports a newer version when one is available.
/// Only active on self-updating (portable desktop) builds; see <see cref="IPlatformStuffService.SupportsSelfUpdate"/>.
/// Fail-silent: any network / parsing error results in <c>null</c>, never an exception to the caller.
/// </summary>
public class UpdateService : IUpdateService
{
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/gritsenko/Pix2d/releases/latest";
    private const string LastCheckSettingKey = "lastUpdateCheckUtc";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly IPlatformStuffService _platformStuffService;
    private readonly ISettingsService _settingsService;

    private UpdateInfo? _cachedResult;

    public UpdateService(IPlatformStuffService platformStuffService, ISettingsService settingsService)
    {
        _platformStuffService = platformStuffService;
        _settingsService = settingsService;
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(bool force = false, CancellationToken ct = default)
    {
        if (!_platformStuffService.SupportsSelfUpdate)
            return null;

        if (!force)
        {
            if (_cachedResult != null)
                return _cachedResult;

            if (!ShouldCheck())
                return null;
        }

        try
        {
            var release = await FetchLatestReleaseAsync(ct);
            SaveLastCheckTime();

            if (release == null)
                return null;

            if (!TryGetCurrentVersion(out var current) || release.Version <= current)
                return null;

            _cachedResult = release;
            return release;
        }
        catch (Exception ex)
        {
            // Update checks must never disturb the user — swallow and log.
            Logger.LogException(ex);
            return null;
        }
    }

    private bool ShouldCheck()
    {
        try
        {
            if (_settingsService.TryGet<string>(LastCheckSettingKey, out var raw)
                && DateTimeOffset.TryParse(raw, out var last))
            {
                return DateTime.UtcNow - last.UtcDateTime >= CheckInterval;
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        return true;
    }

    private void SaveLastCheckTime()
    {
        try
        {
            _settingsService.Set(LastCheckSettingKey, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private async Task<UpdateInfo?> FetchLatestReleaseAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = RequestTimeout };
        // GitHub rejects requests without a User-Agent with 403.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"Pix2d/{_platformStuffService.GetAppVersion()}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var json = await http.GetStringAsync(LatestReleaseApiUrl, ct);
        return ParseRelease(json);
    }

    internal static UpdateInfo? ParseRelease(string json)
    {
        var root = JObject.Parse(json);

        // Ignore drafts and pre-releases — they are not update candidates.
        if (root.Value<bool?>("draft") == true || root.Value<bool?>("prerelease") == true)
            return null;

        var tag = root.Value<string>("tag_name");
        if (!TryParseVersion(tag, out var version))
            return null;

        var name = root.Value<string>("name");
        var body = root.Value<string>("body") ?? string.Empty;
        var htmlUrl = root.Value<string>("html_url") ?? string.Empty;
        var publishedAt = root.Value<DateTimeOffset?>("published_at") ?? DateTimeOffset.MinValue;

        // First downloadable asset (portable archive / installer), if any. Not surfaced in UI yet.
        string? downloadUrl = null;
        if (root["assets"] is JArray assets && assets.Count > 0)
            downloadUrl = assets[0].Value<string>("browser_download_url");

        return new UpdateInfo(
            version,
            string.IsNullOrWhiteSpace(name) ? tag! : name!,
            body.Trim(),
            htmlUrl,
            publishedAt,
            downloadUrl);
    }

    private bool TryGetCurrentVersion(out Version version)
    {
        // Desktop GetAppVersion() returns a clean "x.y.z"; guard against suffixes just in case.
        return TryParseVersion(_platformStuffService.GetAppVersion(), out version);
    }

    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(1);

        // Keep only the leading numeric-dotted portion ("3.8.0 droid" -> "3.8.0").
        var end = 0;
        while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.'))
            end++;
        s = s.Substring(0, end).TrimEnd('.');

        return Version.TryParse(s, out var parsed) && (version = parsed) != null;
    }
}
