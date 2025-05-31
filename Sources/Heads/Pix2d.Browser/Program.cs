using Avalonia;
using Avalonia.Browser;
using Avalonia.Markup.Declarative;
using Microsoft.Extensions.DependencyInjection;
using Pix2d;
using Pix2d.Browser;
using Pix2d.UI;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
    private static readonly ServiceCollection ServiceCollection = [];
    private static void Main(string[] args)
    {
        Console.WriteLine("App started");

        var bootstrapper = new BrowserPix2dBootstrapper();
        Console.WriteLine("Bootstrapper created");
        bootstrapper.ConfigureServices(ServiceCollection);
        var sp = bootstrapper.GetServiceProvider();

        Console.WriteLine("Services configured");
        EditorApp.Pix2dBootstrapper = bootstrapper;
        EditorApp.UiModule = new UiModule();
        EditorApp.AppInitialized = OnAppInitialized;

        BuildAvaloniaApp()
            .UseServiceProvider(sp)
            .StartBrowserAppAsync("out");
    }
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<EditorApp>();

    private static void OnAppInitialized()
    {
        Logger.Log("Application initialized!");
        AppStarted();
    }

    [JSImport("appStarted", "main.js")]
    internal static partial void AppStarted();
}
