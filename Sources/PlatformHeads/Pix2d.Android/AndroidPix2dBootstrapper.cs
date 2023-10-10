using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Android.Services;
using Pix2d.Logging;
using Pix2d.Mvvm;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;

namespace Pix2d.Android;

public class AndroidPix2dBootstrapper : IPix2dBootstrapper
{
    public static Pix2DAppSettings Pix2dSettings { get; } = new()
    {
        AppMode = Pix2DAppMode.SpriteEditor,
        Plugins = new List<Type>
        {
            typeof(SpritePlugin),
            typeof(DrawingPlugin),
            //typeof(AiPlugin)
        },
        AutoSaveNewProject = true,
        AutoSaveInterval = TimeSpan.FromSeconds(10),
    };

    public async Task InitializeAsync()
    {
        if (Pix2DApp.Instance?.IsInitialized == true)
            return;

        if (!Avalonia.Controls.Design.IsDesignMode) Pix2dViewModelBase.SetRuntimeMode();

        var container = IoC.Get<SimpleContainer>();

        container.RegisterSingleton<ILicenseService, FullLicenseService>();
        container.RegisterSingleton<ISettingsService, SettingsService>();
        container.RegisterSingleton<IPlatformStuffService, AndroidPlatformStuffService>();

        container.RegisterSingleton<IFileService, AndroidAvaloniaFileService>();
        container.RegisterSingleton<IClipboardService, InternalClipboardService>();
        container.RegisterSingleton<IFontService, AndroidFontService>();

        container.RegisterInstance<IMessenger>(Messenger.Default);
        container.RegisterSingleton<IDialogService, AvaloniaDialogService>();

        InitiTelemetry();

        Pix2DApp.CurrentPlatform = PlatformType.Android;
        Pix2DApp.AppFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        await Pix2DApp.CreateInstanceAsync(Pix2dSettings);
    }

    private void InitiTelemetry()
    {
#if !DEBUG
        Logger.RegisterLoggerTarget(new SentryLoggerTarget());
#endif
    }
}