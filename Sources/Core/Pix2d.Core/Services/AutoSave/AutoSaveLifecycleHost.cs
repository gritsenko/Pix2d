#nullable enable
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Pix2d.Abstract.Services;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Wires <see cref="IAutoSaveService"/> to the Avalonia application lifecycle so
/// that the editor's session is force-flushed at the latest safe moment on every
/// platform we ship on:
///
/// <list type="bullet">
///   <item><b>Desktop</b> — <see cref="IClassicDesktopStyleApplicationLifetime.MainWindow"/>
///   raises <c>Closing</c> before the process exits.</item>
///
///   <item><b>iOS / Android (Avalonia 11+)</b> — <see cref="IActivatableLifetime"/>
///   raises <see cref="IActivatableLifetime.Deactivated"/> with
///   <see cref="ActivationKind.Background"/> when the OS sends the app into the
///   background. This is the last reliable callback before the OS may freeze /
///   tombstone the process; we MUST commit synchronously inside the handler,
///   not fire-and-forget. Both Android <c>onPause()</c> and iOS
///   <c>applicationWillResignActive</c> map onto this event in Avalonia.</item>
///
///   <item><b>Belt and braces</b> — <see cref="AppDomain.ProcessExit"/> covers
///   Linux SIGTERM and managed-side shutdowns that bypass Avalonia's lifecycle.</item>
/// </list>
///
/// <para>
/// On mobile, head bootstrappers can additionally call <see cref="ForceFlushSync"/>
/// from their native lifecycle hooks (Android <c>Activity.OnPause</c>, iOS
/// <c>AppDelegate.WillResignActive</c>) for an extra layer of safety against
/// tombstoning races. The flush is idempotent.
/// </para>
/// </summary>
public sealed class AutoSaveLifecycleHost : IDisposable
{
    /// <summary>
    /// Window of time we are willing to block the UI thread for a force-save
    /// during a lifecycle transition. Mobile OSes typically give us 1–5 s after
    /// <c>onPause</c> / <c>willResignActive</c> before they may suspend us;
    /// staying well under that keeps the OS happy.
    /// </summary>
    private static readonly TimeSpan BackgroundFlushTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Larger timeout for graceful desktop shutdown.</summary>
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    private readonly IAutoSaveService _autoSave;
    private readonly Application _app;

    private bool _bound;
    private EventHandler<ActivatedEventArgs>? _deactivatedHandler;
    private EventHandler? _processExitHandler;
    private EventHandler<System.ComponentModel.CancelEventArgs>? _mainWindowClosingHandler;

    public AutoSaveLifecycleHost(IAutoSaveService autoSave, Application app)
    {
        _autoSave = autoSave;
        _app = app;
    }

    /// <summary>Subscribes to all available lifecycle signals. Idempotent.</summary>
    public void Bind()
    {
        if (_bound) return;
        _bound = true;

        BindDesktop();
        BindMobile();
        BindProcessExit();
    }

    /// <summary>
    /// Synchronously triggers a force-save on the UI thread, blocking up to the
    /// given timeout. Safe to call from any thread, including platform-native
    /// lifecycle callbacks. Idempotent — multiple calls during the same suspend
    /// transition are coalesced by the auto-save service's internal lock.
    /// </summary>
    public void ForceFlushSync(TimeSpan? timeout = null)
    {
        var window = timeout ?? BackgroundFlushTimeout;
        try
        {
            // We bound the wait. ForceSaveAsync internally already bounds the
            // commit semaphore wait, so the worst case is 2 × window.
            _autoSave.ForceSaveAsync(window).Wait(window);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    // ─────────────── desktop ───────────────

    private void BindDesktop()
    {
        if (_app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        // MainWindow is set lazily by EditorApp; subscribe when available.
        if (desktop.MainWindow is { } mw)
            HookMainWindow(mw);
        else
            desktop.Startup += (_, _) =>
            {
                if (desktop.MainWindow is { } w) HookMainWindow(w);
            };
    }

    private void HookMainWindow(Avalonia.Controls.Window window)
    {
        _mainWindowClosingHandler = (_, _) => ForceFlushSync(ShutdownFlushTimeout);
        window.Closing += _mainWindowClosingHandler;
    }

    // ─────────────── mobile ───────────────

    private void BindMobile()
    {
        // IActivatableLifetime is the cross-platform mobile lifecycle in
        // Avalonia 11+. It is exposed via TryGetFeature on the Application;
        // null on platforms that don't support it (e.g. classic desktop), in
        // which case there is nothing to subscribe.
        var lifetime = _app.TryGetFeature<IActivatableLifetime>();
        if (lifetime is null) return;

        _deactivatedHandler = OnDeactivated;
        lifetime.Deactivated += _deactivatedHandler;
    }

    private void OnDeactivated(object? sender, ActivatedEventArgs e)
    {
        // We only care about real backgrounding events. ActivationKind.Background
        // matches Android onPause / iOS applicationWillResignActive transitions.
        // Pixel-art editors should ignore short focus loss (e.g. notifications);
        // the OS will fire Background separately for those.
        if (e.Kind != ActivationKind.Background) return;

        // The handler runs on the UI thread. We must complete BEFORE returning,
        // because the OS may freeze or kill the process the moment we yield.
        ForceFlushSync(BackgroundFlushTimeout);
    }

    // ─────────────── unconditional belt-and-braces ───────────────

    private void BindProcessExit()
    {
        _processExitHandler = (_, _) => ForceFlushSync(ShutdownFlushTimeout);
        AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
    }

    public void Dispose()
    {
        if (_processExitHandler is not null)
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;

        if (_deactivatedHandler is not null &&
            _app.TryGetFeature<IActivatableLifetime>() is { } lifetime)
        {
            lifetime.Deactivated -= _deactivatedHandler;
        }

        if (_mainWindowClosingHandler is not null &&
            _app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow is { } mw)
        {
            mw.Closing -= _mainWindowClosingHandler;
        }
    }
}
