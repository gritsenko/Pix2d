#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Tools;
using Pix2d.Services;
using Pix2d.CommonNodes;
using Pix2d.Infrastructure;
using Pix2d.Infrastructure.Tasks;
using Pix2d.Logging;
using Pix2d.Plugins.ImageFormats.GifFormat;
using Pix2d.Plugins.ImageFormats.JpgFormat;
using Pix2d.Plugins.ImageFormats.PiskelFormat;
using Pix2d.Plugins.ImageFormats.PngFormat;
using Pix2d.Plugins.ImageFormats.SvgFormat;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.Primitives.Crash;
using Pix2d.Services.Project;
using Pix2d.Services.AutoSave;
using Pix2d.Project;
using Pix2d.Project.AutoSave;
using SkiaNodes.Serialization;
using System.Reflection;
using Pix2d.Command;
using Pix2d.Messages.ViewPort;
using Pix2d.Common.FileSystem;
using Pix2d.Primitives.ViewPort;
using Pix2d.UI;
using Avalonia;
using Avalonia.Threading;
using Pix2d.Infrastructure.AppStat;
using Pix2d.Services.Telemetry;

namespace Pix2d;

public abstract class Pix2dBootstrapperDI : IPix2dBootstrapper
{
    private IServiceCollection? _services;
    private readonly AppState _appState = new AppState();
    private readonly List<Func<IServiceProvider, IPix2dPlugin>> _pluginResolvers = [];
    private IServiceProvider? _serviceProvider;
    private bool _analyticsEnabled;
    private AppStatLoggerTarget? _appStatTarget;

    // Active-session telemetry: the tracker accumulates real usage time (foreground & not idle)
    // regardless of consent (it's inert until reported); the lifecycle host feeds it Avalonia
    // signals; the reporter pushes pings and exists only while analytics is enabled.
    private ActiveTimeTracker? _activeTimeTracker;
    private ActiveSessionLifecycleHost? _activeSessionHost;
    private SessionStatsReporter? _sessionStatsReporter;

    public string? StartupDocument { get; set; }

    protected Pix2dBootstrapperDI()
    {
        // Initialises project-format serialization: assemblies scanned for node types plus the stable
        // $type key registry and legacy aliases. Single source of truth is ProjectFormat (H1.2).
        ProjectFormat.EnsureInitialized([typeof(Pix2dBootstrapperDI).Assembly, typeof(Pix2dSprite).Assembly]);
    }

    public virtual void ConfigureServices(IServiceCollection services)
    {
        _services = services;

        services.AddSingleton(_appState); // No dependencies

        services.AddSingleton<IMessenger>(Messenger.Default); // No dependencies (singleton instance)
        services.AddSingleton<IFontService, AvaloniaFontService>(); // No dependencies
        services
            .AddSingleton<IDialogService,
                AvaloniaDialogService>(); // No explicit dependencies (uses Avalonia internals)

        services.AddSingleton<ISettingsService, SettingsService>(); // Depends on: IPlatformStuffService
        services.AddSingleton<ICrashReportService, CrashReportService>(); // Depends on: IPlatformStuffService, ISettingsService, AppState
        services.AddSingleton<IUpdateService, UpdateService>(); // Depends on: IPlatformStuffService, ISettingsService

        services
            .AddSingleton<IFileService,
                AvaloniaFileService>(); // Depends on: IMessenger, IPlatformStuffService, ISettingsService

        //core pix2d services
        services.AddSingleton<IPaletteService, PaletteService>(); // Depends on: ISettingsService, IFileService
        services.AddSingleton<IEffectsService, EffectsService>(); // no dependencies

        services.AddSingleton<IImportService, ImportService>(); // Depends on: AppState
        services.AddSingleton<Pix2d.Abstract.Services.IOperationDiskCacheService, Pix2d.Services.OperationDiskCacheService>();
        // OperationService takes a Func<IToolService> instead of IToolService directly: IToolService is registered
        // later (line ~99) and several tools depend on IOperationService through DrawingService, so a direct
        // reference would create a construction cycle. The lazy func is resolved at Undo/Redo time, by which
        // point IToolService is fully wired up.
        services.AddSingleton<IOperationService, OperationService>(sp => new OperationService(
            sp.GetRequiredService<AppState>(),
            sp.GetRequiredService<Pix2d.Abstract.Services.IOperationDiskCacheService>(),
            () => sp.GetRequiredService<IToolService>())); // Depends on: AppState, IOperationDiskCacheService, Func<IToolService>
        services.AddSingleton<ISceneService, SceneService>(); // Depends on: AppState, IMessenger
        services.AddSingleton<IViewPortService, ViewPortService>(); // Depends on: IMessenger, AppState
        services.AddSingleton<IViewPortRefreshService, ViewPortRefreshService>(); // Depends on: IViewPortService, IMessenger, AppState
        services.AddSingleton<ILocalizationService, LocalizationService>(); // Depends on: AppState, ISettingsService

        services.AddSingleton<ISnappingService, SnappingService>(); // Depends on: ISceneService, IMessenger, AppState, IViewPortRefreshService
        services.AddSingleton<ISelectionService, SelectionService>(); // Depends on: ISceneService, ISnappingService, IMessenger, AppState

        services.AddSingleton<SpriteEditor>(); //Depends on: IDrawingService, IViewPortRefreshService, IMessenger, AppState, IOperationService
        services.AddSingleton<IEditService, EditService>(); // Depends on: IViewPortRefreshService, IViewPortService, ISelectionService, AppState, IMessenger, SpriteEditor, IOperationService, IDialogService
        services.AddSingleton<ArtboardObjectEditService>(); // Depends on: AppState, IMessenger, IOperationService, IViewPortRefreshService, IEditService, IDrawingService, IDialogService, ISelectionService. Eagerly resolved in SpritePlugin.
        services.AddSingleton<IArtboardObjectEditService>(sp => sp.GetRequiredService<ArtboardObjectEditService>()); // same instance: the concrete type is also resolved directly (SpritePlugin.Initialize)

        services.AddSingleton<IExportService, ExportService>(); // Depends on: AppState, IMessenger, IPlatformStuffService

        // Auto-save subsystem (incremental work-folder + atomic manifest, COW snapshots).
        // Replaces the legacy SessionService. AutoSaveService implements ISessionService
        // as a thin adapter, so existing callers (DesktopPix2dBootstrapperDI.OnAppClosing,
        // MainActivity.SaveSessionSafely, FileCommands.Exit) keep working unchanged.
        services.AddSingleton<IProjectChangeTracker, ProjectChangeTracker>(); // Depends on: IMessenger, AppState
        services.AddSingleton<ISessionSnapshotProvider, UiThreadSnapshotProvider>();
        services.AddSingleton<AutoSaveService>(); // Depends on: AppState, IPlatformStuffService, IMessenger, IProjectChangeTracker, ISessionSnapshotProvider, IProjectActivationService, ICrashReportService
        services.AddSingleton<IAutoSaveService>(sp => sp.GetRequiredService<AutoSaveService>());
        services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<AutoSaveService>());

        services.AddSingleton<IProjectActivationService, ProjectActivationService>(); // Depends on: AppState, IMessenger, IOperationService, IViewPortService, IViewPortRefreshService, IEditService, IServiceProvider (lazy: SpriteEditor, IDrawingService, IProjectChangeTracker)
        services.AddSingleton<IProjectService, ProjectService>(); // Depends on: AppState, IImportService, IMessenger, IFileService, IDialogService, IProjectActivationService, IPlatformStuffService, IOperationService, IAutoSaveService, IExportService
        services.AddSingleton<ISessionProjectLoader, ProjectService>(); // Same as above

        services.AddSingleton<IImportFlowService, Services.Import.ImportFlowService>(); // Depends on: AppState, IImportService, IEditService, IProjectService, IDialogService

        services.AddSingleton<IToolService, ToolService>(sp => new ToolService(sp.GetRequiredService<IMessenger>(),
            sp.GetRequiredService<AppState>(), t => ActivatorUtilities
                .CreateInstance(sp, t))); // Depends on: IMessenger, AppState, Func<Type, ITool>

        services.AddSingleton<ICommandService, CommandService>(); // Depends on: IPlatformStuffService, AppState, IServiceProvider

        //services.AddSingleton<ReviewService>();

        services.AddSingleton<DisableOnAnimationCommandBehavior>(); // Depends on: AppState
        services.AddSingleton<EnableOnClipboardSelectionCommandBehavior>(); // Depends on: AppState, IDrawingService, IMessenger

        // UI scaling service
        services.AddSingleton<IUiScaleService, AvaloniaUiScaleService>();

        // Pen haptics (Surface Slim Pen 2 etc.). Default is a no-op; the desktop head registers a
        // WinRT-backed implementation on Windows after this call, and last-registration-wins for
        // GetService<IPenHapticsService>() means the Windows one is used there.
        services.AddSingleton<IPenHapticsService, NullPenHapticsService>();

        LoadPlugins();
    }

    protected virtual void LoadPlugins()
    {
        LoadPlugin<SpritePlugin>();
        LoadPlugin<PngFormatPlugin>();
        LoadPlugin<JpgFormatPlugin>();
        LoadPlugin<GifFormatPlugin>();
        LoadPlugin<SvgFormatPlugin>();
        // Import-only, pure managed JSON + Skia — no platform dependency, so every head gets it.
        LoadPlugin<PiskelFormatPlugin>();
    }

    protected void LoadPlugin<TPlugin>() where TPlugin : class, IPix2dPlugin
    {
        if (typeof(TPlugin).GetCustomAttribute<ServiceProviderPluginAttribute>() is { } attr)
        {
            _services!.AddSingleton(attr.InterfaceType, attr.InstanceType);
        }

        _services!.AddSingleton<TPlugin>();
        _pluginResolvers.Add(sp => sp.GetRequiredService<TPlugin>());
    }

    public void Initialize()
    {
        var settings = GetPix2dSettings();
        _appState.Settings = settings;

        var serviceProvider = GetServiceProvider();

        InitTelemetry();

        // Crash reporting must come up before anything else can fail.
        InitCrashReporting(serviceProvider);

        // Strict opt-in anonymous analytics/conversion tracking. Runs on every head (independent of the
        // per-head InitTelemetry override) and stays disabled unless a stats endpoint can be resolved
        // from the baked-in DSN AND the user has allowed telemetry.
        InitAnalytics(serviceProvider);

        // React to runtime consent changes (first-launch dialog / crash-dialog toggle / Settings toggle)
        // without waiting for a relaunch: Allowed brings analytics + the crash-telemetry sink up;
        // Denied/Unset stops analytics collection immediately.
        var crashReportService = serviceProvider.GetService<ICrashReportService>();
        if (crashReportService != null)
            crashReportService.TelemetryConsentChanged += consent =>
            {
                if (consent == TelemetryConsent.Allowed)
                {
                    EnableAnalytics(serviceProvider);
                    InitOptionalTelemetry(crashReportService);
                    // A crash recovered from the previous launch may have been detected before consent
                    // existed; now that the sink is up it can finally be sent. Idempotent, so the
                    // duplicate call from InitCrashReporting (consent already Allowed at startup) and
                    // any re-confirmation of Allowed in Settings cost nothing.
                    crashReportService.FlushPendingTelemetry();
                }
                else
                {
                    DisableAnalytics();
                }
            };

        UiBlocker.Initialize((busy, msg) => _appState.IsBusy = busy);

        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        LocalizationHelper.Initialize(serviceProvider.GetRequiredService<ILocalizationService>());

        _appState.UiState.ShowLayers = settingsService.Get<bool>(nameof(AppState.UiState.ShowLayers));
        // Load the persisted UI scale eagerly so MainView.OnAfterInitialized applies the saved
        // value at startup. Previously this happened lazily in AvaloniaUiScaleService's ctor, which
        // is only constructed when the Settings view opens — so on restart the interface stayed 1x.
        _appState.UiScale = settingsService.Get<double?>(nameof(AppState.UiScale)) ?? 1.0;
        _appState.MouseWheelBehavior =  (MouseWheelBehavior) settingsService.Get<int>(nameof(AppState.MouseWheelBehavior));
        _appState.IsTwoFingerDoubleTapUndoEnabled = settingsService.Get<bool?>(nameof(AppState.IsTwoFingerDoubleTapUndoEnabled)) ?? true;
        _appState.TwoFingerDoubleTapTimeoutMs = settingsService.Get<int?>(nameof(AppState.TwoFingerDoubleTapTimeoutMs)) ?? 500;
        _appState.IsStylusModeEnabled = settingsService.Get<bool?>(nameof(AppState.IsStylusModeEnabled)) ?? false;
        _appState.IsSingleFingerPanEnabled = settingsService.Get<bool?>(nameof(AppState.IsSingleFingerPanEnabled)) ?? false;
        _appState.IsPenHapticsEnabled = settingsService.Get<bool?>(nameof(AppState.IsPenHapticsEnabled)) ?? true;
        _appState.IsAutoOpenTransformEditorAfterSelectionEnabled = settingsService.Get<bool?>(nameof(AppState.IsAutoOpenTransformEditorAfterSelectionEnabled)) ?? true;
        _appState.IsReturnToPreviousToolAfterColorPickEnabled = settingsService.Get<bool?>(nameof(AppState.IsReturnToPreviousToolAfterColorPickEnabled)) ?? false;
        _appState.GridColor = GridDefaults.ParseColor(settingsService.Get<string?>(nameof(AppState.GridColor)));
        // Grid nodes are created in DrawingContainerBaseNode's constructor, before any watcher can reach
        // them, so seed the new-node default too (SnappingService keeps it in step afterwards).
        GridDefaults.CurrentColor = _appState.GridColor;

        var commandService = serviceProvider.GetRequiredService<ICommandService>();
        commandService.Initialize();

        var messenger = serviceProvider.GetRequiredService<IMessenger>();
        SessionLogger.InitInstance(messenger);

        // Active-session tracking: measure real usage time (foreground & not idle), not process
        // wall-clock. The tracker/host are created unconditionally (no I/O, no consent implications —
        // data only leaves the device via the reporter, which is consent-gated in EnableAnalytics).
        InitActiveSessionTracking();

        messenger.Register<ViewPortInitializedMessage>(this, msg =>
        {
            TryLoadStartupDocument();
            // The top level is created lazily on some heads (Android); (re)attach once the UI is up.
            _activeSessionHost?.AttachInput(EditorApp.TopLevel);
        });

        InitPlugins(serviceProvider);

        // Force-construct the review service (heads that register one) so its Save/Export subscriptions
        // are live from launch. Otherwise it would only wake up when RatePromptView's optional injection
        // happens to resolve it — a fragile coupling that silently kills the rate prompt if the view
        // stops being built eagerly. Optional: heads without an IReviewService are unaffected.
        serviceProvider.GetService<IReviewService>();
    }

    private void InitPlugins(IServiceProvider serviceProvider)
    {
        foreach (var plugin in _pluginResolvers.Select(pluginResolver => pluginResolver.Invoke(serviceProvider)))
        {
            plugin.Initialize();
        }
    }

    public IServiceProvider GetServiceProvider() => _serviceProvider ??= _services!.BuildServiceProvider();

    private async void TryLoadStartupDocument()
    {
        try
        {
            var sp = _serviceProvider;
            var settings = _appState.Settings;

            if (sp == null) return;

            var appState = sp.GetRequiredService<AppState>();
            // If a real project is already loaded, don't try to recover another
            // session over the top of it. A blank in-memory "new project"
            // created by viewport startup is NOT a loaded project and must not
            // suppress session recovery on desktop / cold app launch.
            if (appState.CurrentProject.SceneNode != null
                && !string.IsNullOrWhiteSpace(appState.CurrentProject.File?.Path))
            {
                MarkLaunchCompletedSafe();
                return;
            }

            //try to load from application startup parameters
            if (StartupDocument != null)
            {
                // Desktop (tabs): restore the previous workspace first, then open the requested
                // document on top of it as its own tab — the same way tabbed editors treat
                // "open file from Explorer". Also starts the autosave loop, which this path
                // previously skipped entirely. Recovery failures must not block the open.
                if (sp.GetRequiredService<IPlatformStuffService>().SupportsMultipleProjects)
                {
                    try
                    {
                        await sp.GetRequiredService<ISessionService>().TryLoadSessionAsync();
                    }
                    catch (Exception sessionEx)
                    {
                        Logger.LogException(sessionEx);
                    }
                }

                var projectService = sp.GetRequiredService<IProjectService>();
                try
                {
                    await projectService.OpenFilesAsync([new NetFileSource(StartupDocument)]);
                }
                catch (Exception openEx)
                {
                    Logger.LogException(openEx);
                }
                MarkLaunchCompletedSafe();
                return;
            }

            //try to load from saved session
            var sessionService = sp.GetRequiredService<ISessionService>();
            try
            {
                await sessionService.TryLoadSessionAsync();
            }
            catch (Exception sessionEx)
            {
                Logger.LogException(sessionEx);
            }
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
        finally
        {
            var sp = _serviceProvider;
            if (sp != null)
            {
                try
                {
                    var appState = sp.GetRequiredService<AppState>();
                    if (appState.CurrentProject.SceneNode == null)
                    {
                        var commandsService = sp.GetRequiredService<ICommandService>();
                        commandsService.GetCommandList<FileCommands>()?.New.Execute();
                    }

                    var viewPortService = sp.GetRequiredService<IViewPortService>();
                    viewPortService.ShowAll();
                }
                catch (Exception finalEx)
                {
                    Logger.LogException(finalEx);
                }
            }

            MarkLaunchCompletedSafe();
        }
    }

    private void MarkLaunchCompletedSafe()
    {
        try
        {
            _serviceProvider?.GetService<ICrashReportService>()?.MarkLaunchCompleted();
        }
        catch
        {
        }
    }

    private void InitCrashReporting(IServiceProvider serviceProvider)
    {
        try
        {
            var crashService = serviceProvider.GetRequiredService<ICrashReportService>();
            crashService.MarkLaunchStarted();

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    HandleFatal(crashService, ex, "AppDomain.UnhandledException");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                HandleFatal(crashService, args.Exception, "TaskScheduler.UnobservedTaskException");
                args.SetObserved();
            };

            try
            {
                Dispatcher.UIThread.UnhandledException += (_, args) =>
                {
                    HandleFatal(crashService, args.Exception, "Dispatcher.UIThread.UnhandledException");
                    args.Handled = true;
                };
            }
            catch
            {
                // Dispatcher may not yet be initialised on all platforms — non-fatal.
            }

            InitOptionalTelemetry(crashService);

            // DetectPendingFromPreviousLaunch ran in the service's constructor, before any sink
            // existed. If it recovered a native crash / ANR from the OS exit record, this is the first
            // moment it can actually be sent.
            crashService.FlushPendingTelemetry();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private static void HandleFatal(ICrashReportService crashService, Exception exception, string source)
    {
        try
        {
            crashService.CaptureFatal(exception, source);
        }
        catch
        {
            // never throw from inside a global handler
        }
    }

    /// <summary>
    /// Hook for platform bootstrappers (currently Android) to wire opt-in Sentry.
    /// Default is no-op so non-Android heads remain free of telemetry dependencies.
    /// </summary>
    protected virtual void InitOptionalTelemetry(ICrashReportService crashService)
    {
    }

    /// <summary>
    /// Brings up anonymous usage analytics — but only when the user has allowed telemetry. Strict
    /// opt-in: on a fresh install (consent Unset) this is a no-op; analytics starts later, the moment
    /// the first-launch consent dialog (or the crash-dialog toggle) flips consent to Allowed, via the
    /// <see cref="ICrashReportService.TelemetryConsentChanged"/> subscription wired in <c>Initialize</c>.
    /// </summary>
    protected virtual void InitAnalytics(IServiceProvider serviceProvider)
    {
        var crashService = serviceProvider.GetService<ICrashReportService>();
        if (crashService?.TelemetryConsent == TelemetryConsent.Allowed)
            EnableAnalytics(serviceProvider);
    }

    /// <summary>
    /// Stands up the active-session accumulator and wires it to Avalonia lifecycle + input signals.
    /// Consent-independent: the tracker only accumulates in-memory time; it is inert until the
    /// consent-gated <see cref="SessionStatsReporter"/> (created in <see cref="EnableAnalytics"/>)
    /// reads it. Guarded so it can never break startup.
    /// </summary>
    private void InitActiveSessionTracking()
    {
        try
        {
            _activeTimeTracker = new ActiveTimeTracker();

            if (Application.Current is { } app)
            {
                _activeSessionHost = new ActiveSessionLifecycleHost(_activeTimeTracker, app)
                {
                    // On mobile the OS may kill us while backgrounded — flush a final ping there.
                    BackgroundReport = () => _sessionStatsReporter?.ReportNow(force: true),
                };
                _activeSessionHost.Bind();
                _activeSessionHost.AttachInput(EditorApp.TopLevel);
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    /// <summary>
    /// Forces a final active-session ping (+ flush). Called from head shutdown/exit hooks
    /// (desktop <c>OnAppClosing</c>, Android double-back exit). No-op when analytics is off.
    /// </summary>
    public void FlushSessionStats()
    {
        try { _sessionStatsReporter?.ReportNow(force: true); }
        catch (Exception ex) { Logger.LogException(ex); }
    }

    /// <summary>
    /// Registers the AppStat analytics logging target (custom events / conversions — never crashes) and
    /// emits the session's first event. The endpoint is derived from the Sentry DSN baked into the
    /// concrete head assembly, so this works uniformly across heads without per-head plumbing; with no
    /// DSN it's a silent no-op. <c>GetType()</c> resolves to the head bootstrapper subclass, so its
    /// <c>.Assembly</c> is the head assembly that carries the <c>SentryDsn</c> metadata. Idempotent:
    /// safe to call both at startup (consent already Allowed) and on a runtime consent grant.
    /// </summary>
    private void EnableAnalytics(IServiceProvider serviceProvider)
    {
        if (_analyticsEnabled)
            return;

        try
        {
            var dsn = AppStatEndpoint.ReadDsn(GetType().Assembly);
            if (!AppStatEndpoint.TryGetTrackUrl(dsn, out var trackUrl))
                return; // no DSN baked in (local/dev builds) → analytics disabled

            var settingsService = serviceProvider.GetService<ISettingsService>();
            var installId = GetOrCreateInstallId(settingsService);

            _appStatTarget = new AppStatLoggerTarget(trackUrl, Pix2d.Common.BuildInfo.Version, installId);
            Logger.RegisterLoggerTarget(_appStatTarget);
            _analyticsEnabled = true;
            Logger.Log("Analytics tracking enabled");

            var platform = serviceProvider.GetService<IPlatformStuffService>()?.CurrentPlatform.ToString();

            // First analytics event of the session: the app started with tracking enabled. The batch
            // envelope already carries release / os / sessionId / installId, so we only add the head
            // platform for segmentation (Android vs desktop vs WASM).
            Logger.LogEventWithParams("App launched", new Dictionary<string, string?>
            {
                { "Platform", platform }
            });

            // Start reporting active-session stats over the same transport. The tracker was created
            // consent-independently in InitActiveSessionTracking; the reporter is what actually sends,
            // so it lives only while analytics is enabled.
            if (_activeTimeTracker != null && _sessionStatsReporter == null)
                _sessionStatsReporter = new SessionStatsReporter(_activeTimeTracker, _appStatTarget, platform);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    /// <summary>
    /// Stops usage analytics mid-session (e.g. the user turns telemetry off in Settings). Flushes any
    /// queued events, then unregisters the AppStat logger target so no further events are collected —
    /// takes effect immediately, not just on the next launch. Crash forwarding is already consent-gated
    /// per capture, so the Sentry sink is left as-is.
    /// </summary>
    private void DisableAnalytics()
    {
        if (!_analyticsEnabled)
            return;

        _analyticsEnabled = false;
        try
        {
            // Stop active-session pings first: send a final one (so the work up to the withdrawal
            // isn't lost) while the target is still registered, then tear the reporter down.
            if (_sessionStatsReporter != null)
            {
                _sessionStatsReporter.ReportNow(force: true);
                _sessionStatsReporter.Dispose();
                _sessionStatsReporter = null;
            }

            if (_appStatTarget != null)
            {
                _appStatTarget.Flush();
                Logger.UnregisterLoggerTarget(_appStatTarget);
                _appStatTarget = null;
            }

            Logger.Log("Analytics tracking disabled");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private static string GetOrCreateInstallId(ISettingsService? settingsService)
    {
        try
        {
            if (settingsService != null
                && settingsService.TryGet<string>(nameof(AppSettings.InstallId), out var existing)
                && !string.IsNullOrEmpty(existing))
                return existing!;

            var id = Guid.NewGuid().ToString("N");
            settingsService?.Set(nameof(AppSettings.InstallId), id);
            return id;
        }
        catch
        {
            // Never let analytics setup break startup — fall back to an ephemeral per-run id.
            return Guid.NewGuid().ToString("N");
        }
    }

    protected virtual bool InitTelemetry()
    {
        Logger.RegisterLoggerTarget(new LocalTextFileLoggerTarget());
        return true;
    }

    public virtual bool OnAppClosing()
    {
        return true;
    }


    protected abstract Pix2DAppSettings GetPix2dSettings();
}
