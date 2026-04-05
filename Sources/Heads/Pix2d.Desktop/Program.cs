using System;
using System.Linq;
using Avalonia;
using Avalonia.Markup.Declarative;
using Pix2d.Services;
using Pix2d.Desktop.Services;
using Pix2d.UI;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.Versioning;

using Microsoft.Win32;

#if DEBUG
[assembly: System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Hot reload uses RequiresUnreferencedCode; only enabled in Debug builds and suppressed for analyzers.")]
[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(HotReloadManager))]
#endif

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
            .UseViewInitializationStrategy(ViewInitializationStrategy.Immediate)
            //.UseManagedSystemDialogs()
            .LogToTrace();


    static void OnAppStarted(object root)
    {
        if (root is MainWindow wnd)
        {
            TouchHelper.ConfigureTouchHandling(wnd);
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

        // Only attempt to associate files on Windows at runtime so analyzers and cross-platform builds don't warn.
        if (OperatingSystem.IsWindows())
        {
            AssociatePix2dFiles();
        }
    }

    [SupportedOSPlatform("windows")]
    private static void AssociatePix2dFiles()
    {
        if (Environment.ProcessPath != null)
            AssociateFileTypeForCurrentUser(".pix2d", "Pix2d.Project", Environment.ProcessPath, "Pix2d Project File");
    }

    [SupportedOSPlatform("windows")]
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
            extKey.SetValue("", progId);
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
            progIdKey.SetValue("", description);
            progIdKey.CreateSubKey(@"DefaultIcon").SetValue("", $"\"{applicationPath}\",0");
            progIdKey.CreateSubKey(@"shell\open\command").SetValue("", $"\"{applicationPath}\" \"%1\"");
        }
    }
}