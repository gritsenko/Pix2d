using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.iOS.Logging;
using Pix2d.iOS.Services;
using Pix2d.Mvvm;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.HttpHost;
using Pix2d.Plugins.PixelText;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;
using Pix2d.UI;

#if !DEBUG
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
#endif

namespace Pix2d.Android;

public class IOSPix2dBootstrapper : IPix2dBootstrapper
{
    public static Pix2DAppSettings Pix2dSettings { get; } = new()
    {
        AppMode = Pix2DAppMode.SpriteEditor,
        Plugins = new List<Type>
        {
            typeof(SpritePlugin),
            typeof(DrawingPlugin),
            typeof(PixelTextPlugin),
            typeof(HttpHostPlugin),
        },
        MainViewType = typeof(MainView),
        AutoSaveInterval = TimeSpan.FromSeconds(10),
        UseInternalFolder = true,
    };

    public async Task InitializeAsync()
    {
        if (Pix2DApp.Instance?.IsInitialized == true)
            return;

        if (!Avalonia.Controls.Design.IsDesignMode) Pix2dViewModelBase.SetRuntimeMode();

        Pix2DApp.CurrentPlatform = PlatformType.Android;
        Pix2DApp.AppFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        var container = IoC.Get<SimpleContainer>();

        container.RegisterSingleton<ISettingsService, SettingsService>();
        container.RegisterSingleton<IPlatformStuffService, IosPlatformStuffService>();

        //container.RegisterSingleton<IFileService, >();
        container.RegisterSingleton<IClipboardService, InternalClipboardService>();
        container.RegisterSingleton<IFontService, IosFontService>();

        container.RegisterInstance<IMessenger>(Messenger.Default);
        container.RegisterSingleton<IDialogService, AvaloniaDialogService>();

        InitLicense(container);

        InitiTelemetry();

        await Pix2DApp.CreateInstanceAsync(Pix2dSettings);
    }

    private void InitLicense(SimpleContainer container)
    {
        container.RegisterSingleton<ILicenseService, FullLicenseService>();
    }


    [Conditional("DEBUG")]
    private void InitiTelemetry()
    {
        AppCenter.Start("78f7d38e-ec8b-43f8-9152-8a4c527827b5", typeof(Analytics), typeof(Crashes));
        Logger.RegisterLoggerTarget(new AppCenterLoggerTarget());
    }
}