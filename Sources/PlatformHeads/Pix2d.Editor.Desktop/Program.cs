using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Markup.Declarative;
using Pix2d.Abstract.Services;

namespace Pix2d.Desktop;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        EditorApp.Pix2dBootstrapper = new DesktopPix2dBootstrapper();
        EditorApp.OnAppStarted = OnAppStarted;
        EditorApp.OnAppClosing = OnAppClosing;
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
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
