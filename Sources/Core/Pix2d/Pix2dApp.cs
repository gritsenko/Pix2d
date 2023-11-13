using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Infrastructure;
using Pix2d.Messages;
using Pix2d.Messages.ViewPort;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d;

public class Pix2DApp : IViewPortService, IAppStateService<AppState>
{
    public static PlatformType CurrentPlatform { get; set; }
    public static string AppFolder { get; set; }


    public event EventHandler ViewPortInitialized;

    private ViewPort _viewPort;

    private readonly Timer _viewPortChangeTimer = new(OnViewportTimerTick, null, -1, -1);

    private readonly Dictionary<Type, IPix2dPlugin> _pluginInstances = new();

    public static Pix2DApp Instance { get; private set; }


    public AppState AppState { get; } = new AppState();

    public static async Task CreateInstanceAsync(Pix2DAppSettings settings)
    {
        await Task.Run(() =>
        {
            Instance = new Pix2DApp();
            Instance.StartupDocument = settings.StartupDocument;
            Instance.Initialize(settings);
        });
    }

    public IFileContentSource StartupDocument { get; set; }

    public ViewPort ViewPort
    {
        get => _viewPort;
        set
        {
            if (_viewPort != null)
                _viewPort.ViewChanged -= ViewPortOnViewChanged;

            _viewPort = value;

            if (_viewPort != null)
            {
                SkiaNodes.AdornerLayer.Initialize(this);
                OnViewPortInitialized();

                if (_viewPort != null)
                    _viewPort.ViewChanged += ViewPortOnViewChanged;
            }
        }
    }

    private static void OnViewportTimerTick(object state)
    {
        Messenger.Default.Send(ViewPortChangedViewMessage.Default);
    }

    private void ViewPortOnViewChanged(object sender, EventArgs e)
    {
        _viewPortChangeTimer.Change(300, -1);
    }


    public bool IsInitialized { get; set; }

    private void OnViewPortInitialized()
    {
        Messenger.Default.Send(ViewPortInitializedMessage.Default);
        ViewPortInitialized?.Invoke(this, EventArgs.Empty);
    }

    public Pix2DApp()
    {
    }

    public void Initialize(Pix2DAppSettings settings = null)
    {
        IsInitialized = true;
        AppState.Settings = settings;

        InitializeCoreServices();
        InitializePluginServices();
        InitializeRefreshEvents();

        Trace("Init tools");
        CoreServices.ToolService.Initialize();

#if DEBUG
        CoreServices.CommandService.RegisterAsyncCommand("Global.SwitchToFullMode",
            () =>
            {
                Instance.SwitchToFullMode();
                return Task.CompletedTask;
            }, "Full Mode",
            new CommandShortcut(VirtualKeys.F12));
#endif


        if (settings?.AppMode == Pix2DAppMode.SpriteEditor)
        {
            Trace("Init sprite mode");
            AppState.CurrentProject.DefaultEditContextType = EditContextType.Sprite;
            AppState.CurrentProject.CurrentContextType = EditContextType.Sprite;
        }

        InitializePlugins();

        CoreServices.CommandService.Initialize();
        CoreServices.PlatformStuffService.SetWindowTitle("Pix2d");
        CoreServices.SettingsService.Set("LaunchTime", DateTime.Now);

        LoadUiSettings();
        
        SessionLogger.InitInstance(Messenger.Default);
    }

    private void LoadUiSettings()
    {
        AppState.UiState.ShowLayers = CoreServices.SettingsService.Get<bool>(nameof(AppState.UiState.ShowLayers));

    }

    public void InitializeCoreServices()
    {
        Trace("Initializing services...");
        Pix2dServiceInitializer.RegisterServiceInstance<IAppStateService<AppState>>(this);
        Pix2dServiceInitializer.RegisterServiceInstance<AppState>(AppState);
        Pix2dServiceInitializer.RegisterServiceInstance<IViewPortService>(this);
        Pix2dServiceInitializer.RegisterServices();
    }

    private void InitializePluginServices()
    {
        Trace("Initialize plugin services...");
        if (AppState.Settings.Plugins == null) return;

        foreach (var pluginType in AppState.Settings.Plugins)
        {
            Trace("\t" + pluginType.Name);
            if (pluginType.GetCustomAttribute(typeof(ServiceProviderPluginAttribute)) is ServiceProviderPluginAttribute attr)
            {
                Pix2dServiceInitializer.RegisterService(attr.InterfaceType, attr.InstanceType);
            }
        }
    }

    public void SwitchToFullMode()
    {
        AppState.CurrentProject.DefaultEditContextType = EditContextType.General;
        AppState.CurrentProject.CurrentContextType = EditContextType.General;

        CoreServices.EditService.ApplyCurrentEdit();
    }


    private void InitializePlugins()
    {
        Trace("Loading plugins...");
        if (AppState.Settings.Plugins == null) return;

        foreach (var pluginType in AppState.Settings.Plugins)
        {
            Trace("\t" + pluginType.Name);
            LoadPlugin(pluginType);
        }
    }

    private void LoadPlugin(Type pluginType)
    {
        var plugin = IoC.Create<IPix2dPlugin>(pluginType);

        _pluginInstances[pluginType] = plugin;

        if (plugin != null)
        {
            plugin.Initialize();
        }
    }

    private void Trace(string message, [CallerMemberName] string caller = null)
    {
#if DEBUG
        Logger.Trace(caller + " : " + message);
#endif
    }

    private void InitializeRefreshEvents()
    {
        Trace("Initializing refresh events...");
        Messenger.Default.Register<ProjectLoadedMessage>(this, m => Refresh());
        Messenger.Default.Register<OperationInvokedMessage>(this, m => Refresh());

        AppState.CurrentProject.ViewPortState.WatchFor(x => x.ShowGrid, Refresh);
        AppState.CurrentProject.ViewPortState.WatchFor(x => x.GridSpacing, Refresh);
        
        //CoreServices.SnappingService.GridToggled += (sender, args) => Refresh();
        //CoreServices.SnappingService.GridCellSizeChanged += (sender, args) => Refresh();
    }

    public void Refresh()
    {
        ViewPort?.Refresh();
    }

    public async void ShowAll()
    {
        var scene = AppState.CurrentProject.SceneNode;
        if (scene != null)
        {
            var bBox = scene.GetBoundingBoxWithContent();
            var vpBBox = ViewPort.Size;
            ViewPort.ShowArea(bBox, new SKSize(vpBBox.Width / 3, vpBBox.Height / 3));
            await Task.Delay(100);
            Refresh();
        }
    }

    public async void OnStartup()
    {
        //If we already have loaded scene
        //android after back button and return
        if (AppState.CurrentProject.SceneNode != null)
        {
            var currentProjectPath = AppState.CurrentProject.File?.Path;
            if (!string.IsNullOrWhiteSpace(currentProjectPath) || AppState.CurrentProject.IsNewProject)
            {
                ShowAll();
                return;
            }
        }

        var ps = CoreServices.ProjectService;
        if (StartupDocument == default)
        {
            if (!await GetService<ISessionService>().TryLoadSessionAsync())
            {
                await ps.CreateNewProjectAsync(new SKSize(64, 64));
            }
        }
        else
        {
            await ps.OpenFilesAsync(new[] { StartupDocument });
        }
    }

    public void Dispose()
    {
        foreach (var pluginInstance in _pluginInstances)
        {
            if (pluginInstance.Value is IDisposable disposablePlugin)
            {
                disposablePlugin.Dispose();
            }
        }
    }

    public async Task OnAppClosed()
    {

        await GetService<ISessionService>().SaveSessionAsync();

        var launchTime = CoreServices.SettingsService.Get<DateTime>("LaunchTime");
        var curTime = DateTime.Now;
        var delta = curTime - launchTime;

        var totalTime = new TimeSpan();

        CoreServices.SettingsService.TryGet("TotalWorkTime", out totalTime);

        totalTime += delta;

        CoreServices.SettingsService.Set<TimeSpan>("TotalWorkTime", totalTime);
    }

    private static T GetService<T>() => CommonServiceLocator.ServiceLocator.Current.GetInstance<T>();

}