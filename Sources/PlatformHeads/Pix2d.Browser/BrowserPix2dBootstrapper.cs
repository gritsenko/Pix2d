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
using Pix2d.Browser.Services;
using Pix2d.Mvvm;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.PixelText;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;
using Pix2d.UI;

namespace Pix2d.Browser
{
    public class BrowserPix2dBootstrapper : IPix2dBootstrapper
    {
        public static Pix2DAppSettings Pix2dSettings { get; } = new()
        {
            AppMode = Pix2DAppMode.SpriteEditor,
            Plugins = new List<Type>
            {
                typeof(SpritePlugin),
                typeof(DrawingPlugin),
                typeof(PixelTextPlugin),
            },
            MainViewType = typeof(MainView)
        };

        public async Task InitializeAsync()
        {
            if (Pix2DApp.Instance?.IsInitialized == true)
                return;
            
            if (!global::Avalonia.Controls.Design.IsDesignMode) Pix2dViewModelBase.SetRuntimeMode();
            
            var container = IoC.Get<SimpleContainer>();
            container.RegisterInstance<IMessenger>(Messenger.Default);

            container.RegisterSingleton<ILicenseService, FullLicenseService>();
            container.RegisterSingleton<ISettingsService, BlazorSettingsService>();
            container.RegisterSingleton<IFileService, BrowserFileService>();
            container.RegisterSingleton<IClipboardService, InternalClipboardService>();
            container.RegisterSingleton<IPlatformStuffService, BlazorPlatformStuffService>();
            container.RegisterSingleton<IDialogService, AvaloniaDialogService>();

            Pix2DApp.CurrentPlatform = PlatformType.WASM;
            Pix2DApp.AppFolder = System.IO.Path.Combine(AppDataFolder(), "Pix2d");

            //if (!Directory.Exists(Pix2DApp.AppFolder))
            //{
            //    Directory.CreateDirectory(Pix2DApp.AppFolder);
            //}

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
}