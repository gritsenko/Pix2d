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
using Pix2d.Services;

namespace Pix2d.Droid;

public class AndroidPix2dBootstrapper : Pix2dBootstrapperDI
{
    protected override Pix2DAppSettings GetPix2dSettings()
    {
        IFileContentSource? startupDoc = string.IsNullOrWhiteSpace(StartupDocument) ? null : new NetFileSource(StartupDocument); //for regular file paths (local app path)

        if (StartupDocument?.StartsWith("content:") == true)  // for external files 
            startupDoc = new AndroidFileContentSource(Android.Net.Uri.Parse(StartupDocument));

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
        services.AddSingleton<IClipboardService, InternalClipboardService>();
        services.AddSingleton<IFontService, AndroidFontService>();
        services.AddSingleton<IReviewService, AndroidReviewService>();

        services.AddSingleton<ILicenseService, PlayMarketLicenseService>();
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