using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Browser.Services;
using Pix2d.Infrastructure.Logger;
using Pix2d.Plugins.BaseEffects;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.PixelText;
using Pix2d.Services;

namespace Pix2d.Browser;

public class BrowserPix2dBootstrapper : Pix2dBootstrapperDI
{
    protected override Pix2DAppSettings GetPix2dSettings() => new()
    {
        AppMode = Pix2DAppMode.SpriteEditor,
    };

    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services); // Calls base class registration (see Pix2dBootstrapperDI)

        services.AddSingleton<IPlatformStuffService, BrowserPlatformStuffService>(); // Depends on: AppState
        services.AddSingleton<IClipboardService, InternalClipboardService>(); // Depends on: IDrawingService, IViewPortService, IDialogService, AppState
        services.AddSingleton<ISettingsService, BrowserSettingsService>();
        services.AddSingleton<IFileService, BrowserFileService>();
    }

    protected override void LoadPlugins()
    {
        //load core plugins from Pix2d.Core assembly
        base.LoadPlugins();

        //load plugins from external assemblies
        LoadPlugin<BaseEffectsPlugin>();
        LoadPlugin<DrawingPlugin>();
        LoadPlugin<PixelTextPlugin>();
    }

    protected override bool InitTelemetry()
    {
        //we are not calling base InitTelemetry because it uses logging to file
        Logger.RegisterLoggerTarget(new ConsoleLoggerTarget());
        Logger.Log("Console logging enabled");

        return true;
    }
}