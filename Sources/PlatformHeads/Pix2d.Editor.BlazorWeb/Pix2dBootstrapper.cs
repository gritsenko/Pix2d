using System.Reflection;
using System.Runtime.InteropServices;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.BlazorWasm.Services;
using Pix2d.Mvvm;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;

namespace Pix2d.BlazorWasm
{
    public class Pix2dBootstrapper : IPix2dBootstrapper
    {
        public static Pix2DAppSettings Pix2dSettings { get; } = new()
        {
            AppMode = Pix2DAppMode.SpriteEditor,
            Plugins = new List<Type>
            {
                typeof(SpritePlugin),
                typeof(DrawingPlugin),
            }
        };

        public async Task InitializeAsync()
        {
            if (Pix2DApp.Instance?.IsInitialized == true)
                return;
            
            if (!global::Avalonia.Controls.Design.IsDesignMode) Pix2dViewModelBase.SetRuntimeMode();
            
            var container = IoC.Get<SimpleContainer>();

            container.RegisterSingleton<ISettingsService, BlazorSettingsService>();
            //container.RegisterSingleton<IFileService, AvaloiaNetFileService>();
            container.RegisterSingleton<IClipboardService, InternalClipboardService>();

            container.RegisterInstance<IMessenger>(Messenger.Default);

            container.RegisterSingleton<IPlatformStuffService, BlazorPlatformStuffService>();
            //container.RegisterSingleton<IDialogService, NetDialogService>();

            Pix2DApp.CurrentPlatform = PlatformType.Avalonia;

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