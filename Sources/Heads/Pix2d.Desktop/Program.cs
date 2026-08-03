using Avalonia;
using Avalonia.Markup.Declarative;
#if DEBUG
using Declarative.Avalonia.AgentTools;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Pix2d.Desktop.Services;
using Pix2d.Services;
using Pix2d.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
#if DEBUG
        // LogToTrace() writes Avalonia's warnings (backend selection, GPU blocklists, binding errors)
        // to Trace, which without a listener is only visible under a debugger — mirror it to the
        // console so `dotnet run` shows the same diagnostics as an IDE session.
        Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
        Trace.AutoFlush = true;
#endif

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

#if DEBUG
        // In-process MCP inspector (Declarative.Avalonia.AgentTools) — loopback streamable-HTTP
        // server on http://127.0.0.1:5599 exposing get_visual_tree / list_components / screenshot_*
        // / get_errors, plus the opt-in `invoke` remote-control tool. Debug-only; must never ship
        // in Release (the package reference is Debug-gated in the .csproj).
        builder = builder.UseAgentInspector(o => o.EnableInteraction = true);
#endif

        return ConfigureWindowsRendering(builder);
    }

    /// <summary>
    /// Picks the Win32 GPU backend order. Avalonia's default is [AngleEgl, Software], which is right
    /// on x64 — but on Windows-on-ARM (Snapdragon X / Adreno) Avalonia's ANGLE-D3D11 path hits its
    /// Adreno driver blocklist and logs
    /// <c>"ARM64 Adreno GPU detected; the Adreno rendering blocklist is forcing a fallback to
    /// 'microsoft basic render driver'"</c>, i.e. the editor silently renders on the WARP software
    /// adapter. Wgl and Vulkan both avoid the blocklist and both run on the real Adreno adapter, so
    /// ask for those first and keep AngleEgl / Software behind them as fallbacks.
    /// <para>
    /// <b>Wgl leads, not Vulkan</b>, even though Vulkan is the Adreno driver's native API: the two
    /// measured the same here (~20 ms vs ~22 ms CPU per canvas frame), but Avalonia's Vulkan backend
    /// threw out of <c>VulkanSkiaGpu.TryCreateRenderTarget</c> on the render thread during real use
    /// on this hardware, which kills the render loop mid-session. Wgl resolves to Windows' inbox
    /// GLon12 layer (no vendor GL ICD is registered on Snapdragon) and, like ANGLE, presents through
    /// DXGI — the better-trodden path. Vulkan stays second so a device without GLon12 still gets the
    /// GPU. Flip the order at runtime with <c>PIX2D_RENDERING_MODE</c> to compare.
    /// </para>
    /// <para>
    /// Composition mode is deliberately left at Avalonia's default: WinUIComposition and
    /// DirectComposition only apply to AngleEgl, and the default list already ends with
    /// RedirectionSurface, which is what Wgl and Vulkan land on.
    /// </para>
    /// <para>
    /// <c>PIX2D_RENDERING_MODE</c> (comma-separated <c>vulkan,wgl,angle,software</c>) overrides the
    /// order on any Windows machine — an escape hatch for a driver that renders wrong on the backend
    /// chosen here, without needing a patched build.
    /// </para>
    /// </summary>
    private static AppBuilder ConfigureWindowsRendering(AppBuilder builder)
    {
        if (!OperatingSystem.IsWindows())
            return builder;

        var modes = ParseRenderingModes(Environment.GetEnvironmentVariable("PIX2D_RENDERING_MODE"));

        if (modes == null)
        {
            // Note: OSArchitecture, not ProcessArchitecture — an x64 build running under ARM64
            // emulation talks to the same Adreno driver and gets blocklisted the same way.
            if (RuntimeInformation.OSArchitecture != Architecture.Arm64)
                return builder;

            modes =
            [
                Win32RenderingMode.Wgl,
                Win32RenderingMode.Vulkan,
                Win32RenderingMode.AngleEgl,
                Win32RenderingMode.Software
            ];
        }

        return builder.With(new Win32PlatformOptions { RenderingMode = modes });
    }

    private static IReadOnlyList<Win32RenderingMode>? ParseRenderingModes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var modes = new List<Win32RenderingMode>();
        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Win32RenderingMode? mode = token.ToLowerInvariant() switch
            {
                "vulkan" => Win32RenderingMode.Vulkan,
                "wgl" or "opengl" => Win32RenderingMode.Wgl,
                "angle" or "angleegl" or "egl" => Win32RenderingMode.AngleEgl,
                "software" or "cpu" => Win32RenderingMode.Software,
                _ => null
            };

            if (mode is { } m && !modes.Contains(m))
                modes.Add(m);
        }

        if (modes.Count == 0)
            return null;

        // Never let a typo in the variable leave the app with no backend it can start on.
        if (!modes.Contains(Win32RenderingMode.Software))
            modes.Add(Win32RenderingMode.Software);

        return modes;
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
        // Force a full repaint after every hot reload (Avalonia leaves descendants stale until resize).
        HotReloadRepaint.Install();
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