using Avalonia;
using Avalonia.Markup.Declarative;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Pix2d.Desktop.Services;
using Pix2d.Services;
using Pix2d.UI;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

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
            .UseComponentControlFactory(type => CreateComponentControl(sp, type))
            .StartWithClassicDesktopLifetime(args);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Desktop builds resolve Avalonia declarative views dynamically through the control factory; desktop publishing currently does not trim output.")]
    private static Avalonia.Controls.Control CreateComponentControl(IServiceProvider serviceProvider, Type type)
    {
        return (Avalonia.Controls.Control)ActivatorUtilities.CreateInstance(serviceProvider, type);
    }

    // CrossPlatformDesktop configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<EditorApp>()
                .UsePlatformDetect()
                .UseViewInitializationStrategy(ViewInitializationStrategy.Immediate)
                .LogToTrace();

        // Проверяем, запущено ли приложение на Windows и архитектуре ARM64
        /*if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            builder.With(new Win32PlatformOptions
            {
                RenderingMode = [
                    Win32RenderingMode.Wgl,
                    Win32RenderingMode.AngleEgl,
                    Win32RenderingMode.Vulkan,
                    Win32RenderingMode.Software
                ],
                CompositionMode = [
                    Win32CompositionMode.WinUIComposition,
                    Win32CompositionMode.DirectComposition
                ]
            });
        }
*/
        return builder;
    }

    static void OnAppStarted(object root)
    {
        if (root is MainWindow wnd)
        {
            TouchHelper.ConfigureTouchHandling(wnd);
        }
    }

    private static void OnAppInitialized(EditorApp editorApp)
    {
#if WINDOWS_UWP
        UwpPlatformStuffService.InitStoreContext();
#endif

        // Only attempt to associate files on Windows at runtime so analyzers and cross-platform builds don't warn.
        if (OperatingSystem.IsWindows())
        {
            AssociatePix2dFiles();
        }

#if DEBUG
        editorApp.AttachDeveloperTools();
#endif

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