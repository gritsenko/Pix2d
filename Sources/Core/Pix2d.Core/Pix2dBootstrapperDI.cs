#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Tools;
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
using Pix2d.Services;
using Pix2d.Services.Project;
using SkiaNodes.Serialization;
using System.Reflection;
using Pix2d.Command;
using Pix2d.Messages.ViewPort;

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

        services
            .AddSingleton<IFileService,
                AvaloniaFileService>(); // Depends on: IMessenger, IPlatformStuffService, ISettingsService

        //core pix2d services
        services.AddSingleton<IPaletteService, PaletteService>(); // no dependencies
        services.AddSingleton<IEffectsService, EffectsService>(); // no dependencies

        services.AddSingleton<IImportService, ImportService>(); // Depends on: AppState
        services.AddSingleton<IOperationService, OperationService>(); // Depends on: AppState
        services.AddSingleton<ISceneService, SceneService>(); // Depends on: AppState, IMessenger
        services.AddSingleton<IViewPortService, ViewPortService>(); // Depends on: IMessenger, AppState
        services.AddSingleton<IViewPortRefreshService, ViewPortRefreshService>(); // Depends on: IViewPortService, IMessenger, AppState
        services.AddSingleton<ILocalizationService, LocalizationService>(); // Depends on: AppState, ISettingsService

        services.AddSingleton<ISnappingService, SnappingService>(); // Depends on: ISceneService, IMessenger, AppState
        services.AddSingleton<ISelectionService, SelectionService>(); // Depends on: ISceneService, ISnappingService, IMessenger, AppState

        services.AddSingleton<SpriteEditor>(); //Depends on: IDrawingService, IViewPortRefreshService, IMessenger, AppState, IOperationService
        services.AddSingleton<IEditService, EditService>(); // Depends on: IViewPortRefreshService, IViewPortService, ISelectionService, AppState, IMessenger, SpriteEditor

        services.AddSingleton<IObjectCreationService, ObjectCreationService>(); // Depends on: ISelectionService, ISceneService

        services.AddSingleton<IExportService, ExportService>(); // Depends on: AppState, IMessenger, IPlatformStuffService

        services.AddSingleton<ISessionService, SessionService>(); // Depends on: ISessionProjectLoader, AppState, IFileService, ISettingsService
        services.AddSingleton<IProjectService, ProjectService>(); // Depends on: AppState, IImportService, IMessenger
        services.AddSingleton<ISessionProjectLoader, ProjectService>(); // Same as above

        services.AddSingleton<IToolService, ToolService>(sp => new ToolService(sp.GetRequiredService<IMessenger>(),
            sp.GetRequiredService<AppState>(), t => ActivatorUtilities
                .CreateInstance(sp, t))); // Depends on: IMessenger, AppState, Func<Type, ITool>

        services.AddSingleton<ICommandService, CommandService>(); // Depends on: IPlatformStuffService, AppState, IServiceProvider

        //services.AddSingleton<ReviewService>();

        services.AddSingleton<DisableOnAnimationCommandBehavior>(); // Depends on: AppState

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
        UiBlocker.Initialize((busy, msg) => _appState.IsBusy = busy);

        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        _appState.UiState.ShowLayers = settingsService.Get<bool>(nameof(AppState.UiState.ShowLayers));

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

            var appState = sp.GetRequiredService<AppState>();
            //If we already have loaded scene
            //case: android after back button and return
            if (appState.CurrentProject.SceneNode != null
                && (!string.IsNullOrWhiteSpace(appState.CurrentProject.File?.Path) ||
                    appState.CurrentProject.IsNewProject))
                return;

            //try to load from application startup parameters
            if (StartupDocument != null)
            {
                var projectService = sp.GetRequiredService<IProjectService>();
                await projectService.OpenFilesAsync([settings.StartupDocument]);
                return;
            }

            //try to load from saved session
            var sessionService = sp.GetRequiredService<ISessionService>();
            await sessionService.TryLoadSessionAsync();
        }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
        finally
        {
            var sp = _serviceProvider;
            var appState = sp.GetRequiredService<AppState>();
            if (appState.CurrentProject.SceneNode == null)
            {
                var commandsService = sp.GetRequiredService<ICommandService>();
                commandsService.GetCommandList<FileCommands>()?.New.Execute();
            }

            var viewPortService = sp.GetRequiredService<IViewPortService>();
            viewPortService.ShowAll();
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