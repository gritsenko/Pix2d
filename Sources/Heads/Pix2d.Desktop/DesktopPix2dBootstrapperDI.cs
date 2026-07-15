using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Desktop.Services;
using Pix2d.Infrastructure.Logger;
using Pix2d.Plugins.Ai;
using Pix2d.Plugins.BaseEffects;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.PixelText;
using Pix2d.Primitives.Crash;
using Pix2d.Services;
using System;
using System.Threading.Tasks;

namespace Pix2d.Desktop;

public class DesktopPix2dBootstrapperDI : Pix2dBootstrapperDI // Inherits: Pix2dBootstrapperDI (depends on: none directly, but see base class)
{
    protected override Pix2DAppSettings GetPix2dSettings() => new()
    {
        AppMode = Pix2DAppMode.SpriteEditor,
        StartupDocument = string.IsNullOrWhiteSpace(StartupDocument) ? null : new NetFileSource(StartupDocument)
    };

    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services); // Calls base class registration (see Pix2dBootstrapperDI)

        services.AddSingleton<IPlatformStuffService, PlatformStuffService>(); // Depends on: AppState
        services.AddSingleton<IClipboardService, DesktopClipboardService>(); // Depends on: IDrawingService, IViewPortService, IDialogService, AppState
        services.AddSingleton<IReviewService, DesktopReviewService>(); // Depends on: ISettingsService, IMessenger, AppState, IPlatformStuffService

        // Opt-in critical-crash telemetry sink (Sentry). Registered on every desktop OS; stays a no-op
        // until the user allows anonymous crash reporting and a DSN was baked into the build.
        services.AddSingleton<ICrashTelemetrySink, DesktopSentryCrashTelemetrySink>();

#if WINDOWS
        // Replaces the no-op IPenHapticsService registered by the base bootstrapper. Win11-only API is
        // guarded inside the service, so this is harmless on older Windows (feature just stays off).
        services.AddSingleton<IPenHapticsService, Platform.WindowsPenHapticsService>();
#endif
    }

    protected override void InitOptionalTelemetry(ICrashReportService crashService)
    {
        // Strict opt-in: only initialise the Sentry sink once the user has explicitly allowed
        // anonymous crash reporting. Until then we do nothing — the local crash report flow
        // still works.
        if (crashService.TelemetryConsent != TelemetryConsent.Allowed)
            return;

        try
        {
            var sink = GetServiceProvider().GetService(typeof(ICrashTelemetrySink)) as ICrashTelemetrySink;
            sink?.Initialize();
        }
        catch
        {
        }
    }
    
    protected override void LoadPlugins()
    {
        base.LoadPlugins();

        LoadPlugin<BaseEffectsPlugin>();
        LoadPlugin<DrawingPlugin>();
        LoadPlugin<PixelTextPlugin>();
        LoadPlugin<AiPlugin>();
        //LoadPlugin<HttpHostPlugin>();
        //LoadPlugin<OpenCvPlugin>();
        //LoadPlugin<PsdPlugin>();
        //LoadPlugin<CollaboratePlugin>();
        //LoadPlugin<OpenGlPlugin>();
    }
    
    public override bool OnAppClosing()
    {
        // Mark a deliberate shutdown so the next launch doesn't mistake this exit for an interrupted
        // launch / crash. Desktop has no OS process-exit info, so without this marker a normal close
        // could surface a phantom "empty" crash report on relaunch.
        try
        {
            GetServiceProvider().GetService<ICrashReportService>()?.MarkCleanExit();
        }
        catch
        {
        }

        var autoSave = GetServiceProvider().GetRequiredService<IAutoSaveService>();
        autoSave.ForceSaveSync(TimeSpan.FromSeconds(5));
        return true;
    }

    protected override bool InitTelemetry()
    {
        Logger.RegisterLoggerTarget(new ConsoleLoggerTarget());
        Logger.Log("Console logging enabled");

        return base.InitTelemetry();
    }
}
