using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Common.FileSystem;
using SkiaNodes.Interactive;

namespace Pix2d.Services;

public class PlatformStuffService : IPlatformStuffService
{
    private readonly IServiceProvider _serviceProvider;

    public PlatformStuffService(AppState state, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        state.PropertyChanged += (_, p) =>
        {
            if (p.PropertyName == nameof(state.WindowTitle)) SetWindowTitle(state.WindowTitle);
        };
        SingleInstancePipeService.MessageReceived += SingleInstancePipeService_MessageReceived;
        EnsureAppFolderExists();
    }

    private void EnsureAppFolderExists()
    {
        var path = GetAppFolderPath();

        if (string.IsNullOrWhiteSpace(path))
            throw new Exception("Data folder is not initialized");

        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private void SingleInstancePipeService_MessageReceived(object? sender, string e)
    {
        Dispatcher.UIThread.Invoke(async () =>
        {
            if (string.IsNullOrWhiteSpace(e))
                return;

            var args = JsonConvert.DeserializeObject<string[]>(e);
            if (args == null || !args.Any())
                return;

            var file = args.LastOrDefault();

            if (string.IsNullOrWhiteSpace(file) || !file.ToLower().EndsWith("pix2d"))
                return;

            var projectService = _serviceProvider.GetRequiredService<IProjectService>();

            if (projectService == null)
                return;

            var fileSource = new NetFileSource(file);
            await projectService.OpenFilesAsync([fileSource]);
        });
    }


    public PlatformType CurrentPlatform => PlatformType.CrossPlatformDesktop;

    public void OpenUrlInBrowser(string url)
    {
        //System.Diagnostics.Process.Start(url);
        //Process.Start("chrome.exe", url);
        OpenBrowser(url);
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    public void SetWindowTitle(string title)
    {
        if (EditorApp.TopLevel is MainWindow wnd)
        {
            Dispatcher.UIThread.Post(() => { wnd.Title = title + " - Pix2d v" + GetAppVersion(); });
        }
    }

    public MemoryInfo GetMemoryInfo()
    {
        var proc = Process.GetCurrentProcess();
        var mem = proc.PrivateMemorySize64;
        var available = Environment.WorkingSet;
        return new MemoryInfo((ulong)available, (ulong)mem);
    }

    public string KeyToString(VirtualKeys key)
    {
        switch (key)
        {
            case VirtualKeys.OEM4:
                return "[";
            case VirtualKeys.OEM6:
                return "]";
            case VirtualKeys.OEMPlus:
                return "=";
            case VirtualKeys.OEMMinus:
                return "-";
            case VirtualKeys.N0:
                return "0";
            case VirtualKeys.OEMPeriod:
                return ".";
            case VirtualKeys.OEMComma:
                return ",";
        }

        return key.ToString();
    }

    public string GetAppVersion()
    {
        try
        {
            var appPath = AppContext.BaseDirectory;
            var asm = this.GetType().Assembly.GetName().Version;
            if (asm != null)
            {
                return $"{asm.Major}.{asm.Minor}.{asm.Build}";
            }

            var assemblyPath = Path.Combine(appPath, "pix2d.exe");
            var fvi = FileVersionInfo.GetVersionInfo(assemblyPath);
            var version = fvi.ProductVersion;
            return version;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        return "unknown desktop";
    }

    public void ToggleTopmostWindow()
    {
        if (EditorApp.TopLevel is MainWindow wnd)
        {
            wnd.Topmost = !wnd.Topmost;
        }
    }

    public bool HasKeyboard => true;
    public bool CanShare => false;

    public void Share(IStreamExporter exporter, double scale = 1)
    {
        throw new NotSupportedException();
    }

    public void ToggleFullscreenMode()
    {
        if (EditorApp.TopLevel is MainWindow wnd)
        {
            wnd.WindowState = wnd.WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
        }
    }

    public string GetAppFolderPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pix2d");

    public Task OpenAppDataFolder()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(folder, "Pix2dLogs");

        if (!Directory.Exists(appFolder))
        {
            try
            {
                Directory.CreateDirectory(appFolder);
                LogInfo($"Created log directory: {appFolder}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to create log directory: {appFolder}", ex);
                return Task.CompletedTask;
            }
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", $"\"{appFolder}\"");
                LogInfo($"Opened folder on Windows: {appFolder}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", $"\"{appFolder}\"");
                LogInfo($"Opened folder on Linux: {appFolder}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", $"\"{appFolder}\"");
                LogInfo($"Opened folder on macOS: {appFolder}");
            }
            else
            {
                LogInfo($"Opening application data folder is not supported on {RuntimeInformation.OSDescription}.");
            }
        }
        catch (Exception ex)
        {
            LogError($"Failed to launch file browser for folder: {appFolder}", ex);
        }

        return Task.CompletedTask;

        void LogInfo(string message) => Console.WriteLine($"INFO: {message}");

        void LogError(string message, Exception ex = null)
        {
            Console.WriteLine($"ERROR: {message} - {ex?.Message}");
            if (ex != null) Console.WriteLine(ex.ToString());
        }
    }
}