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
using Pix2d.Plugins.ImageFormats.PngFormat;
using Pix2d.Plugins.ImageFormats.SvgFormat;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.Services.Project;
using Pix2d.Services.AutoSave;
using Pix2d.Project.AutoSave;
using SkiaNodes.Serialization;
using System.Reflection;
using Pix2d.Command;
using Pix2d.Messages.ViewPort;
using Pix2d.Common.FileSystem;
using Pix2d.Primitives.ViewPort;
using Pix2d.UI;
using Avalonia.Threading;

namespace Pix2d;

public abstract class Pix2dBootstrapperDI : IPix2dBootstrapper
{
    private IServiceCollection? _services;
    private readonly AppState _appState = new AppState();
    private readonly List<Func<IServiceProvider, IPix2dPlugin>> _pluginResolvers = [];
    private IServiceProvider? _serviceProvider;

    public string? StartupDocument { get; set; }

    protected Pix2dBootstrapperDI()
    {
        // Used to correctly serialize nodes types into project json
        NodeSerializer.ExtraAssemblies = [typeof(Pix2dBootstrapperDI).Assembly, typeof(Pix2dSprite).Assembly];
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

        services
            .AddSingleton<IFileService,
                AvaloniaFileService>(); // Depends on: IMessenger, IPlatformStuffService, ISettingsService

        //core pix2d services
        services.AddSingleton<IPaletteService, PaletteService>(); // no dependencies
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

        services.AddSingleton<ISnappingService, SnappingService>(); // Depends on: ISceneService, IMessenger, AppState
        services.AddSingleton<ISelectionService, SelectionService>(); // Depends on: ISceneService, ISnappingService, IMessenger, AppState

        services.AddSingleton<SpriteEditor>(); //Depends on: IDrawingService, IViewPortRefreshService, IMessenger, AppState, IOperationService
        services.AddSingleton<IEditService, EditService>(); // Depends on: IViewPortRefreshService, IViewPortService, ISelectionService, AppState, IMessenger, SpriteEditor

        services.AddSingleton<IExportService, ExportService>(); // Depends on: AppState, IMessenger, IPlatformStuffService

        // Auto-save subsystem (incremental work-folder + atomic manifest, COW snapshots).
        // Replaces the legacy SessionService. AutoSaveService implements ISessionService
        // as a thin adapter, so existing callers (DesktopPix2dBootstrapperDI.OnAppClosing,
        // MainActivity.SaveSessionSafely, FileCommands.Exit) keep working unchanged.
        services.AddSingleton<IProjectChangeTracker, ProjectChangeTracker>(); // Depends on: IMessenger, AppState
        services.AddSingleton<ISessionSnapshotProvider, UiThreadSnapshotProvider>();
        services.AddSingleton<AutoSaveService>(); // Depends on: AppState, IPlatformStuffService, IMessenger, IProjectChangeTracker, ISessionSnapshotProvider
        services.AddSingleton<IAutoSaveService>(sp => sp.GetRequiredService<AutoSaveService>());
        services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<AutoSaveService>());

        services.AddSingleton<IProjectService, ProjectService>(); // Depends on: AppState, IImportService, IMessenger
        services.AddSingleton<ISessionProjectLoader, ProjectService>(); // Same as above

        services.AddSingleton<IToolService, ToolService>(sp => new ToolService(sp.GetRequiredService<IMessenger>(),
            sp.GetRequiredService<AppState>(), t => ActivatorUtilities
                .CreateInstance(sp, t))); // Depends on: IMessenger, AppState, Func<Type, ITool>

        services.AddSingleton<ICommandService, CommandService>(); // Depends on: IPlatformStuffService, AppState, IServiceProvider

        //services.AddSingleton<ReviewService>();

        services.AddSingleton<DisableOnAnimationCommandBehavior>(); // Depends on: AppState
        services.AddSingleton<EnableOnClipboardSelectionCommandBehavior>(); // Depends on: AppState, IDrawingService, IMessenger

        // UI scaling service
        services.AddSingleton<IUiScaleService, AvaloniaUiScaleService>();

        LoadPlugins();
    }

    protected virtual void LoadPlugins()
    {
        LoadPlugin<SpritePlugin>();
        LoadPlugin<PngFormatPlugin>();
        LoadPlugin<JpgFormatPlugin>();
        LoadPlugin<GifFormatPlugin>();
        LoadPlugin<SvgFormatPlugin>();
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
        _appState.IsAutoOpenTransformEditorAfterSelectionEnabled = settingsService.Get<bool?>(nameof(AppState.IsAutoOpenTransformEditorAfterSelectionEnabled)) ?? true;

        var commandService = serviceProvider.GetRequiredService<ICommandService>();
        commandService.Initialize();

        var messenger = serviceProvider.GetRequiredService<IMessenger>();
        SessionLogger.InitInstance(messenger);
        messenger.Register<ViewPortInitializedMessage>(this, msg => TryLoadStartupDocument());

        InitPlugins(serviceProvider);

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
