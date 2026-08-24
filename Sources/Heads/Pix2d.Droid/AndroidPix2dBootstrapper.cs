using System;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Droid.Services;
using Pix2d.Plugins.Ai;
using Pix2d.Plugins.BaseEffects;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.PixelText;
using Pix2d.Primitives.Crash;
using Pix2d.Services;

namespace Pix2d.Droid;

public class AndroidPix2dBootstrapper : Pix2dBootstrapperDI
{
    /// <summary>
    /// A `content://` URI from a file manager is not a path, so it needs the SAF-backed source.
    /// This runs when the document is opened rather than at settings time, because Avalonia (and with
    /// it <c>Initialize</c>) starts in <c>Application.OnCreate</c>, before <c>MainActivity.OnCreate</c>
    /// knows the URI — resolving it eagerly always produced null here.
    /// </summary>
    protected override IFileContentSource? ResolveStartupDocument(string document)
    {
        if (document.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
        {
            var uri = Android.Net.Uri.Parse(document);
            return uri == null ? null : new AndroidFileContentSource(uri);
        }

        return base.ResolveStartupDocument(document); // a plain path inside the app's own folders
    }

    protected override Pix2DAppSettings GetPix2dSettings()
    {
        var startupDoc = string.IsNullOrWhiteSpace(StartupDocument) ? null : ResolveStartupDocument(StartupDocument);

        return new Pix2DAppSettings
        {
            AppMode = Pix2DAppMode.SpriteEditor,
            AutoSaveInterval = TimeSpan.FromSeconds(30),
            StartupDocument = startupDoc,
            UseInternalFolder = true,
        };
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        services.AddSingleton<IPlatformStuffService, AndroidPlatformStuffService>();
        services.AddSingleton<IFileService, AndroidAvaloniaFileService>();
        services.AddSingleton<IClipboardService, AndroidClipboardService>();
        services.AddSingleton<IFontService, AndroidFontService>();
        services.AddSingleton<IReviewService, AndroidReviewService>();

        services.AddSingleton<ILicenseService, PlayMarketLicenseService>();
        services.AddSingleton<ICrashTelemetrySink, AndroidSentryCrashTelemetrySink>();
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
        //load core plugins from Pix2d.Core assembly
        base.LoadPlugins();

        //load plugins from external assemblies
        LoadPlugin<BaseEffectsPlugin>();
        LoadPlugin<DrawingPlugin>();
        LoadPlugin<PixelTextPlugin>();
        LoadPlugin<AiPlugin>();
    }
}