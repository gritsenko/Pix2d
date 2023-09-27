using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Common;
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

        Crashes.GetErrorAttachments = report =>
        {
            return new[]
            {
                ErrorAttachmentLog.AttachmentWithText(SessionLogger.Instance.GetSessionOperationLogText(),
                    "operations.log")
            };
        };

        var ff = RuntimeInformation.FrameworkDescription;
        var container = IoC.Get<SimpleContainer>();

        container.RegisterSingleton<ISettingsService, SettingsService>();
        container.RegisterSingleton<IFileService, AvaloiaFileService>();
        container.RegisterSingleton<IClipboardService, AvaloniaClipboardService>();
        container.RegisterSingleton<IFontService, AvaloniaFontService>();

        container.RegisterInstance<IMessenger>(Messenger.Default);

        container.RegisterSingleton<IPlatformStuffService, PlatformStuffService>();
        container.RegisterSingleton<IDialogService, AvaloniaDialogService>();


        Pix2DApp.CurrentPlatform = GetPlatform();
        Pix2DApp.AppFolder = Path.Combine(AppDataFolder(), "Pix2d");

        var licenseName = await InitLicense(container);
        InitTelemetry(Pix2DApp.CurrentPlatform);

        if (!Directory.Exists(Pix2DApp.AppFolder))
        {
            Directory.CreateDirectory(Pix2DApp.AppFolder);
        }

        await Pix2DApp.CreateInstanceAsync(Pix2dSettings);

        if (Pix2DApp.Instance != null)
        {
            Pix2DApp.Instance.CurrentLicense = licenseName;
        }
    }

    private async Task<string> InitLicense(SimpleContainer container)
    {
        var licenseName = "Free";

        ILicenseService? licenseService = null;
#if WINDOWS_UWP
        var uwpLicenseService = new Pix2d.WindowsStore.Services.UwpLicenseService();
        await uwpLicenseService.Init();
        licenseService = uwpLicenseService;
#endif

        if (licenseService == null) return licenseName;

        if (licenseService.IsPro)
            licenseName = "Pro";

        container.RegisterInstance<ILicenseService>(licenseService);

        return licenseName;
    }

    private PlatformType GetPlatform()
    {
#if WINDOWS_UWP
        return PlatformType.WindowsStore;
#elif WINFORMS
        return PlatformType.WindowsDesktop;
#endif
        return PlatformType.CrossPlatformDesktop;
    }

    private void InitTelemetry(PlatformType platform)
    {
#if DEBUG
        return;
#endif

        if (platform == PlatformType.WindowsStore)
        {
            Debug.WriteLine("Register appcenter for UWP");
            AppCenter.Start("3d4da0e3-1840-4181-858d-cbf6ecadac55", typeof(Analytics), typeof(Crashes));
            Logger.RegisterLoggerTarget(new AppCenterLoggerTarget());
        }
        else if (platform == PlatformType.WindowsDesktop)
        {
            Debug.WriteLine("Register appcenter for winforms");
            AppCenter.Start("2c0dc23b-1bcd-42dc-b7c2-d6944fab2c58", typeof(Analytics), typeof(Crashes));
            Logger.RegisterLoggerTarget(new AppCenterLoggerTarget());
        }
        else
        {
#if !WINDOWS_UWP
            Logger.RegisterLoggerTarget(new SentryLoggerTarget());
#endif
        }
        //Logger.RegisterLoggerTarget(new GALoggerTarget("G-K2TCKSBBCX", "LOVC5ToFRJ2-b54hKgDiaQ"));
    }

    Type? GetTypeByName(string name) => AppDomain.CurrentDomain.GetAssemblies().Reverse().Select(assembly => assembly.GetType(name)).FirstOrDefault(tt => tt != null);

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