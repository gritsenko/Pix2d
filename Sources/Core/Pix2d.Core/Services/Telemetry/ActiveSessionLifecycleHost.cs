#nullable enable
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Pix2d.Infrastructure.AppStat;

namespace Pix2d.Services.Telemetry;

/// <summary>
/// Feeds Avalonia lifecycle + input signals into <see cref="ActiveTimeTracker"/> so it can
/// measure real active usage time (foreground &amp; not idle) rather than process wall-clock.
///
/// Modelled on <see cref="AutoSave.AutoSaveLifecycleHost"/> — same desktop-vs-mobile branching:
/// <list type="bullet">
///   <item><b>Desktop</b> — <see cref="Window.Activated"/> / <see cref="Window.Deactivated"/> on
///   the main window are the foreground signal. Unlike autosave (which only cares about a real
///   background transition) we WANT to stop counting on plain focus loss (alt-tab), so we use the
///   window events directly.</item>
///   <item><b>Mobile (Android/iOS)</b> — <see cref="IActivatableLifetime"/> raises
///   <see cref="IActivatableLifetime.Activated"/> / <see cref="IActivatableLifetime.Deactivated"/>
///   with <see cref="ActivationKind.Background"/> on (un)backgrounding.</item>
///   <item><b>WASM</b> — no foreground signal is available; the tracker stays foreground and the
///   idle timeout alone bounds active time. Acceptable for v1.</item>
/// </list>
///
/// Input handlers are attached to a <see cref="TopLevel"/> via <see cref="AttachInput"/>. Because
/// the top level is created at different times per head (immediately on desktop/WASM, later on
/// Android), the bootstrapper re-drives <see cref="AttachInput"/> once the UI is up
/// (on <c>ViewPortInitializedMessage</c>); re-attaching the same top level is a no-op.
/// </summary>
public sealed class ActiveSessionLifecycleHost : IDisposable
{
    private readonly ActiveTimeTracker _tracker;
    private readonly Application _app;

    private bool _bound;
    private TopLevel? _inputTopLevel;

    private EventHandler<ActivatedEventArgs>? _activatedHandler;
    private EventHandler<ActivatedEventArgs>? _deactivatedHandler;

    /// <summary>
    /// Invoked when the app is backgrounded (mobile) — the bootstrapper wires this to force a final
    /// session-stats ping, since the OS may kill the process before any later callback runs.
    /// </summary>
    public Action? BackgroundReport { get; set; }

    public ActiveSessionLifecycleHost(ActiveTimeTracker tracker, Application app)
    {
        _tracker = tracker;
        _app = app;
    }

    /// <summary>Subscribes to the available foreground signals. Idempotent.</summary>
    public void Bind()
    {
        if (_bound) return;
        _bound = true;

        BindDesktop();
        BindMobile();
    }

    /// <summary>
    /// Attaches input handlers (pointer / key / wheel) to the given top level. Safe to call
    /// repeatedly — a null or already-hooked top level is ignored; a new one replaces the old.
    /// </summary>
    public void AttachInput(TopLevel? topLevel)
    {
        if (topLevel is null || ReferenceEquals(topLevel, _inputTopLevel))
            return;

        DetachInput();
        _inputTopLevel = topLevel;

        // Tunnel + handledEventsToo so we see every input even when a child control handles it.
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerMovedEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.PointerWheelChangedEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
        topLevel.AddHandler(InputElement.KeyDownEvent, OnInput, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnInput(object? sender, RoutedEventArgs e) => _tracker.NotifyInput();

    // ─────────────── desktop ───────────────

    private void BindDesktop()
    {
        if (_app.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        // MainWindow is set lazily by EditorApp; subscribe when available (same pattern as
        // AutoSaveLifecycleHost).
        if (desktop.MainWindow is { } mw)
            HookWindow(mw);
        else
            desktop.Startup += (_, _) =>
            {
                if (desktop.MainWindow is { } w) HookWindow(w);
            };
    }

    private void HookWindow(Window window)
    {
        window.Activated += (_, _) => _tracker.NotifyForeground(true);
        window.Deactivated += (_, _) => _tracker.NotifyForeground(false);
        AttachInput(window); // a Window is a TopLevel
    }

    // ─────────────── mobile ───────────────

    private void BindMobile()
    {
        var lifetime = _app.TryGetFeature<IActivatableLifetime>();
        if (lifetime is null) return;

        _activatedHandler = OnActivated;
        _deactivatedHandler = OnDeactivated;
        lifetime.Activated += _activatedHandler;
        lifetime.Deactivated += _deactivatedHandler;
    }

    private void OnActivated(object? sender, ActivatedEventArgs e)
    {
        // Coming back from the background (Android onResume / iOS didBecomeActive).
        if (e.Kind == ActivationKind.Background)
            _tracker.NotifyForeground(true);
    }

    private void OnDeactivated(object? sender, ActivatedEventArgs e)
    {
        // Real backgrounding only (Android onPause / iOS willResignActive); short focus loss is
        // reported with a different kind and must not stop the clock spuriously.
        if (e.Kind != ActivationKind.Background)
            return;

        _tracker.NotifyForeground(false);

        // The process may be frozen/killed while backgrounded — flush a final ping now.
        try { BackgroundReport?.Invoke(); } catch { /* telemetry must never break lifecycle */ }
    }

    private void DetachInput()
    {
        if (_inputTopLevel is not { } tl)
            return;

        tl.RemoveHandler(InputElement.PointerPressedEvent, OnInput);
        tl.RemoveHandler(InputElement.PointerMovedEvent, OnInput);
        tl.RemoveHandler(InputElement.PointerWheelChangedEvent, OnInput);
        tl.RemoveHandler(InputElement.KeyDownEvent, OnInput);
        _inputTopLevel = null;
    }

    public void Dispose()
    {
        DetachInput();

        if (_app.TryGetFeature<IActivatableLifetime>() is { } lifetime)
        {
            if (_activatedHandler is not null) lifetime.Activated -= _activatedHandler;
            if (_deactivatedHandler is not null) lifetime.Deactivated -= _deactivatedHandler;
        }
    }
}
