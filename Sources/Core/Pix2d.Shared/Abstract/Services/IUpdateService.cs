#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Pix2d.Primitives;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Checks whether a newer Pix2d release is available for self-updating (portable desktop) builds.
/// The source of truth is the GitHub releases page. Store / Android / WASM builds are updated by
/// their platform, so on those <see cref="IPlatformStuffService.SupportsSelfUpdate"/> is false and
/// the check is skipped.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Returns information about a newer release, or <c>null</c> when the app is up to date, the
    /// platform does not support self-update, the check was throttled, or the request failed.
    /// Never throws — network / parsing failures are swallowed and logged.
    /// </summary>
    /// <param name="force">Ignore the once-per-day throttle (used by the manual "Check for updates" button).</param>
    Task<UpdateInfo?> CheckForUpdateAsync(bool force = false, CancellationToken ct = default);
}
