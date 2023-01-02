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
using Pix2d.Editor.Desktop.Services;
using Pix2d.Mvvm;
using Pix2d.Plugins.Ai;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.HttpHost;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;

namespace Pix2d.Editor.Desktop;

public class Pix2dBootstrapper : IPix2dBootstrapper
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

        var container = IoC.Get<SimpleContainer>();

        container.RegisterSingleton<ISettingsService, SettingsService>();
        container.RegisterSingleton<IFileService, AvaloiaNetFileService>();
        container.RegisterSingleton<IClipboardService, AvaloniaClipboardService>();

        container.RegisterInstance<IMessenger>(Messenger.Default);

        container.RegisterSingleton<IPlatformStuffService, PlatformStuffService>();
        container.RegisterSingleton<IDialogService, AvaloniaDialogService>();

        Pix2DApp.CurrentPlatform = PlatformType.Avalonia;

        Pix2DApp.AppFolder = Path.Combine(AppDataFolder(), "Pix2d");

        if (!Directory.Exists(Pix2DApp.AppFolder))
        {
            Directory.CreateDirectory(Pix2DApp.AppFolder);
        }

        ReadAppSettingsConfig(Pix2dSettings);

        await Pix2DApp.CreateInstanceAsync(Pix2dSettings);
    }

    private void ReadAppSettingsConfig(Pix2DAppSettings pix2dSettings)
    {
        //try
        //{
        //    var appSettings = ConfigurationManager.AppSettings;

        //    var pluginsStr = appSettings["Plugins"];

        //    if (pluginsStr != null)
        //    {
        //        var plugins = pluginsStr.Split(';');
        //        foreach ( var pluginName in plugins) {
        //            var pluginType = FindModuleType(pluginName);
        //            if(pluginType != null) {
        //                pix2dSettings.Plugins.Add(pluginType);
        //            }
        //        }
        //    }
        //}
        //catch (ConfigurationErrorsException)
        //{
        //    Debug.WriteLine("Error reading app settings");
        //}
    }

    private static Type FindModuleType(string assemblyName)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName);
        var tt = assembly.GetTypes().FirstOrDefault(x => x.IsAssignableTo(typeof(IPix2dPlugin)));
        if (tt != null)
        {
            return tt;
        }

        return null;
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