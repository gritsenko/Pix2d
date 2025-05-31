using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Declarative;
using Pix2d.Services;
using Pix2d.UI;
using Microsoft.Extensions.DependencyInjection;

#if Windows || WINDOWS_UWP
using Microsoft.Win32;
#endif

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

        var bootstrapper = new DesktopPix2dBootstrapperDI()
        {
            StartupDocument = args.FirstOrDefault()
        };


        ServiceCollection serviceCollection = [];
        bootstrapper.ConfigureServices(serviceCollection);
        var sp = bootstrapper.GetServiceProvider();

        EditorApp.Pix2dBootstrapper = bootstrapper;
        EditorApp.AppStarted = OnAppStarted;
        EditorApp.AppInitialized = OnAppInitialized;
        EditorApp.UiModule = new UiModule();
        EditorApp.OnAppClosing = bootstrapper.OnAppClosing;

        BuildAvaloniaApp()
            .UseServiceProvider(sp)
            .StartWithClassicDesktopLifetime(args);
    }

    // CrossPlatformDesktop configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<EditorApp>()
            .UsePlatformDetect()
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

    private static void OnAppInitialized()
    {
#if WINDOWS_UWP
        UwpPlatformStuffService.InitStoreContext();
#endif

#if Windows || WINDOWS_UWP
        AssociatePix2dFiles();
#endif
    }


#if Windows || WINDOWS_UWP
    private static void AssociatePix2dFiles()
    {
        if (Environment.ProcessPath != null)
            AssociateFileTypeForCurrentUser(".pix2d", "Pix2d.Project", Environment.ProcessPath, "Pix2d Project File");
    }

    private static void AssociateFileTypeForCurrentUser(string extension, string progId, string applicationPath, string description)
    {
        using (var extKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}", writable: false))
        {
            if (extKey != null && extKey.GetValue("")?.ToString() == progId)
            {
                return;
            }
        }

        using (var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}"))
        {
            extKey?.SetValue("", progId);
        }

        using (var progIdKey = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}", writable: false))
        {
            if (progIdKey != null)
            {
                return;
            }
        }

        using (var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
        {
            progIdKey?.SetValue("", description);
            progIdKey?.CreateSubKey(@"DefaultIcon")?.SetValue("", $"\"{applicationPath}\",0");
            progIdKey?.CreateSubKey(@"shell\open\command")?.SetValue("", $"\"{applicationPath}\" \"%1\"");
        }
    }
#endif
}