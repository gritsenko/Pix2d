#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Infrastructure.Logger;
using Pix2d.Plugins.Ai;
using Pix2d.Plugins.BaseEffects;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.PixelText;
using Pix2d.Plugins.PngCompress;
using Pix2d.Services;
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
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>(); // Depends on: IDrawingService, IViewPortService, IDialogService, AppState
    }
    
    protected override void LoadPlugins()
    {
        base.LoadPlugins();

        LoadPlugin<BaseEffectsPlugin>();
        LoadPlugin<DrawingPlugin>();
        LoadPlugin<PixelTextPlugin>();
        LoadPlugin<PngCompressPlugin>();
        LoadPlugin<AiPlugin>();
        //LoadPlugin<HttpHostPlugin>();
        //LoadPlugin<OpenCvPlugin>();
        //LoadPlugin<PsdPlugin>();
        //LoadPlugin<CollaboratePlugin>();
        //LoadPlugin<OpenGlPlugin>();
    }
    
    public override bool OnAppClosing()
    {
        var sessionService = GetServiceProvider().GetRequiredService<ISessionService>();
        Task.Run(() => sessionService.TrySaveSessionAsync()).GetAwaiter().GetResult();
        return true;
    }

    protected override bool InitTelemetry()
    {
        Logger.RegisterLoggerTarget(new ConsoleLoggerTarget());
        Logger.Log("Console logging enabled");

        return base.InitTelemetry();
    }
}
