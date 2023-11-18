using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Avalonia.Markup.Declarative;
using Pix2d;
using Pix2d.Browser;
using Pix2d.UI;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static void Main(string[] args)
    {
        EditorApp.Pix2dBootstrapper = new BrowserPix2dBootstrapper();
        EditorApp.UiModule = new UiModule();

        BuildAvaloniaApp()
            .UseServiceProvider(DefaultServiceLocator.ServiceLocatorProvider())
            //.UseReactiveUI()
            .StartBrowserAppAsync("out");


    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<EditorApp>();
}