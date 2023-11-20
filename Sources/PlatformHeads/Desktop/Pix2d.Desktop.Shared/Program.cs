using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Services;
using Pix2d.Desktop.Services;
using Pix2d.UI;
using Sentry;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Avalonia.Markup.Declarative.HotReloadManager))]

namespace Pix2d.Desktop;
class Program
{
    // Initialization code. Don't use any CrossPlatformDesktop, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (!OperatingSystem.IsMacOS())
            SingleInstancePipeService.CheckSingleInstance();

        //DispatcherUnhandledException += App_DispatcherUnhandledException;

        EditorApp.Pix2dBootstrapper = new DesktopPix2dBootstrapper()
        {
            StartupDocument = args.FirstOrDefault()
        };
        EditorApp.OnAppStarted = OnAppStarted;
        EditorApp.OnAppInitialized = OnAppInitialized;
        EditorApp.OnAppClosing = OnAppClosing;
        EditorApp.UiModule = new UiModule();

        try
        {
            var isActive = SentrySdk.IsEnabled;
            SentrySdk.CaptureMessage("Pix2d started");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            SentrySdk.CaptureException(e);
            // here we can work with the exception, for example add it to our log file
            Logger.LogException(e, "Unhandled exception happened");
            throw;
        }
        finally
        {
            // This block is optional. 
            // Use the finally-block if you need to clean things up or similar
            SentrySdk.Flush();
        }

    }

    // CrossPlatformDesktop configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<EditorApp>()
            .UsePlatformDetect()
            .UseServiceProvider(DefaultServiceLocator.ServiceLocatorProvider())
            //.UseManagedSystemDialogs()
            .LogToTrace();

    private static bool OnAppClosing()
    {
        SentrySdk.Flush();

        var ss = CommonServiceLocator.ServiceLocator.Current.GetInstance<ISessionService>();
        if (ss != null)
        {
            Task.Run(() => ss.SaveSessionAsync()).GetAwaiter().GetResult();
        }
        return true;
    }

    static void OnAppStarted(object root)
    {
        if (root is MainWindow wnd)
        {
#if DEBUG
            wnd.AttachDevTools();
#endif
        }
    }
    private static void OnAppInitialized()
    {
#if WINDOWS_UWP
        UwpPlatformStuffService.InitStoreContext();
#endif
    }

}
