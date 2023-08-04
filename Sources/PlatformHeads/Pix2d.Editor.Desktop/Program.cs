using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Services;
using Sentry;
using Sentry.Protocol;

namespace Pix2d.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        //DispatcherUnhandledException += App_DispatcherUnhandledException;
        SentrySdk.Init(o =>
        {
            // Tells which project in Sentry to send events to:
            o.Dsn = "https://9088a22c17385d098701c8059b42f460@o4505646237614080.ingest.sentry.io/4505646271692800";
            // When configuring for the first time, to see what the SDK is doing:
            //o.Debug = true;
            // Set TracesSampleRate to 1.0 to capture 100% of transactions for performance monitoring.
            // We recommend adjusting this value in production.
            //o.TracesSampleRate = 1.0;
            o.SendClientReports = false;
        });

        SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new User
            {
                Email = "john.doe@example.com"
            };
        });

        EditorApp.Pix2dBootstrapper = new DesktopPix2dBootstrapper();
        EditorApp.OnAppStarted = OnAppStarted;
        EditorApp.OnAppClosing = OnAppClosing;

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
        }
        finally
        {
            // This block is optional. 
            // Use the finally-block if you need to clean things up or similar
            SentrySdk.Flush();
        }

    }

    // Avalonia configuration, don't remove; also used by visual designer.
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
}
