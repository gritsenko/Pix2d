using System;
using Avalonia;
using Avalonia.Markup.Declarative;

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
