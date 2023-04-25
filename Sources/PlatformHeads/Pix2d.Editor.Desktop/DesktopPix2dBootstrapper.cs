using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Desktop.Logging;
using Pix2d.Desktop.Services;
using Pix2d.Editor.Desktop.Services;
using Pix2d.Mvvm;
using Pix2d.Plugins.Ai;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.HttpHost;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;

namespace Pix2d.Desktop;

public class DesktopPix2dBootstrapper : IPix2dBootstrapper
{
    public static Pix2DAppSettings Pix2dSettings { get; } = new()
    {
        AppMode = Pix2DAppMode.SpriteEditor,
        Plugins = new List<Type>
        {
            typeof(SpritePlugin),
            typeof(DrawingPlugin),
            typeof(HttpHostPlugin),
            //typeof(SpinePlugin),
            typeof(AiPlugin),
        }
    };

    public async Task InitializeAsync()
    {
        if (Pix2DApp.Instance?.IsInitialized == true)
            return;

        if (!Avalonia.Controls.Design.IsDesignMode) Pix2dViewModelBase.SetRuntimeMode();

        Logger.RegisterLoggerTarget(new AppStatLoggerTarget());

        var container = IoC.Get<SimpleContainer>();

        container.RegisterSingleton<ISettingsService, SettingsService>();
        container.RegisterSingleton<IFileService, AvaloiaFileService>();
        container.RegisterSingleton<IClipboardService, AvaloniaClipboardService>();
        container.RegisterSingleton<IFontService, AvaloniaFontService>();

        container.RegisterInstance<IMessenger>(Messenger.Default);

        container.RegisterSingleton<IPlatformStuffService, PlatformStuffService>();
        container.RegisterSingleton<IDialogService, AvaloniaDialogService>();

        Pix2DApp.CurrentPlatform = PlatformType.Avalonia;

        Pix2DApp.AppFolder = Path.Combine(AppDataFolder(), "Pix2d");

        if (!Directory.Exists(Pix2DApp.AppFolder))
        {
            Directory.CreateDirectory(Pix2DApp.AppFolder);
        }

        await Pix2DApp.CreateInstanceAsync(Pix2dSettings);
    }

    public static string AppDataFolder()
    {
        var userPath = Environment.GetEnvironmentVariable(
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
                "LOCALAPPDATA" : "HOME");

        var assy = Assembly.GetEntryAssembly();
        var companyName = assy.GetCustomAttributes<AssemblyCompanyAttribute>()
            .FirstOrDefault();
        var path = Path.Combine(userPath, companyName.Company);

        return path;
    }

}