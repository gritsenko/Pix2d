#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.Services;
using Pix2d.Services;
using Pix2d.State;
#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Services.Store;
#endif

namespace Pix2d.Desktop.Services;

/// <summary>
/// Desktop implementation of the in-app "rate the app" flow. Reuses all of <see cref="ReviewService"/>'s
/// timing/heuristics (work-time gate, defer schedule, Save/Export triggers) and only decides <b>where</b>
/// the rating goes:
/// <list type="bullet">
///   <item><b>Microsoft Store (MSIX) build</b> → the native Store review dialog
///     (<c>StoreContext.RequestRateAndReviewAppAsync</c>), falling back to the Store review page via
///     <c>ms-windows-store://review</c> if the native API is unavailable.</item>
///   <item><b>Portable / itch.io / Gumroad / Linux / macOS</b> → the <c>pix2d.com/review</c> hub. The
///     binary is identical across those channels and can't know which one it came from, so the hub — not
///     the app — picks the destination, and it can change without a new release.</item>
/// </list>
/// The native Store code is compiled only into the Windows build (<c>#if WINDOWS</c>, which already
/// targets a WinRT-projecting TFM for pen haptics); Linux/macOS stay on plain <c>net10.0</c> and never see it.
/// </summary>
public class DesktopReviewService(
    ISettingsService settingsService,
    IMessenger messenger,
    AppState appState,
    IPlatformStuffService platformStuff)
    : ReviewService(settingsService, messenger, appState)
{
    private readonly IPlatformStuffService _platformStuff = platformStuff;

    protected override async Task<bool> RateAppCore()
    {
        try
        {
#if WINDOWS
            if (_platformStuff.IsStorePackage)
            {
                var status = await TryRequestStoreReviewAsync();
                if (status != null)
                {
                    // Native Store rating dialog was shown; `result` is the user's response
                    // (Succeeded / CanceledByUser / NetworkError / Error) — the true conversion signal.
                    LogReview("Store dialog", extra: new Dictionary<string, string> { ["result"] = status });
                    return status == nameof(StoreRateAndReviewStatus.Succeeded);
                }

                // Native in-app review unavailable (pre-1809 Windows, missing Store identity, or no window
                // handle) — open the Store's review page directly via protocol activation.
                _platformStuff.OpenUrlInBrowser($"ms-windows-store://review/?ProductId={StoreProductId}");
                LogReview("Opened store page");
                return true;
            }
#endif
            // Non-store desktop: route through the site hub (see class summary).
            _platformStuff.OpenUrlInBrowser(BuildReviewHubUrl());
            LogReview("Opened review hub", extra: new Dictionary<string, string>
            {
                ["dest"] = "hub",
                ["pkg"] = _platformStuff.IsStorePackage ? "store" : "portable",
            });
#if !WINDOWS
            await Task.CompletedTask; // no awaited work on this build; keeps the async signature warning-free
#endif
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return false;
        }
    }

    private string BuildReviewHubUrl()
    {
        var os = OperatingSystem.IsWindows() ? "win"
            : OperatingSystem.IsMacOS() ? "mac"
            : OperatingSystem.IsLinux() ? "linux"
            : "desktop";
        var pkg = _platformStuff.IsStorePackage ? "store" : "portable";
        var version = Uri.EscapeDataString(_platformStuff.GetAppVersion() ?? "");
        return $"https://pix2d.com/review?src=desktop&os={os}&pkg={pkg}&v={version}";
    }

#if WINDOWS
    // Microsoft Store product id for Pix2d (MSStoreCLIAppId in Package.appxmanifest). Used for both the
    // native review request and the ms-windows-store://review fallback.
    private const string StoreProductId = "9NBLGGH1ZDFV";

    /// <summary>
    /// Shows the native Microsoft Store rating dialog. Returns the <see cref="StoreRateAndReviewStatus"/>
    /// as a string (the user's response) when the dialog was shown, or <c>null</c> when it could not be
    /// shown (no window handle / missing Store identity / API error) so the caller can fall back.
    /// </summary>
    [SupportedOSPlatform("windows10.0.17763.0")]
    private static async Task<string?> TryRequestStoreReviewAsync()
    {
        try
        {
            var hwnd = EditorApp.TopLevel?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero)
                return null;

            var context = StoreContext.GetDefault();
            // A desktop (non-UWP) StoreContext must be associated with a window before it shows any UI.
            ((IInitializeWithWindow)(object)context).Initialize(hwnd);

            var result = await context.RequestRateAndReviewAppAsync();
            return result.Status.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return null;
        }
    }

    [ComImport, Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IInitializeWithWindow
    {
        void Initialize(IntPtr hwnd);
    }
#endif
}
