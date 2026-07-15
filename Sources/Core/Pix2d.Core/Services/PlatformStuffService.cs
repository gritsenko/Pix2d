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
    public PlatformType CurrentPlatform => PlatformType.CrossPlatformDesktop;
    public bool SupportsMultipleProjects => true;

    // Portable builds self-update from GitHub; the Store (MSIX) build is updated by the Store. The same
    // Pix2d.Desktop binary ships in both, so this is decided at runtime by MSIX package identity.
    private bool? _supportsSelfUpdate;
    public bool SupportsSelfUpdate => _supportsSelfUpdate ??= !IsRunningAsPackagedApp();

    // The MS Store build ships the same Pix2d.Desktop binary as the portable one; MSIX package identity
    // (checked at runtime) is what tells them apart — see IsRunningAsPackagedApp.
    private bool? _isStorePackage;
    public bool IsStorePackage => _isStorePackage ??= IsRunningAsPackagedApp();

    public bool IsTextInputFocused => EditorApp.TopLevel?.FocusManager?.GetFocusedElement() is TextBox;

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
        if (EditorApp.TopLevel is Window wnd)
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

    public string GetAppVersion() => Pix2d.Common.BuildInfo.Version;

    private const int AppmodelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);

    /// <summary>
    /// True when the process runs inside an MSIX package (Microsoft Store build). On non-Windows
    /// desktops there is no MSIX packaging, so this is always false (portable → self-update enabled).
    /// </summary>
    private static bool IsRunningAsPackagedApp()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            return result != AppmodelErrorNoPackage;
        }
        catch
        {
            // API missing (very old Windows) → treat as unpackaged/portable.
            return false;
        }
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
        Path.Combine(GetLocalApplicationDataPath(), "Pix2d");

    /// <summary>
    /// Resolves the per-user local application-data directory in a cross-platform-safe way.
    /// On Linux, <see cref="Environment.GetFolderPath(Environment.SpecialFolder)"/> for
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/> returns an EMPTY string when
    /// <c>~/.local/share</c> does not exist yet (default <see cref="Environment.SpecialFolderOption.None"/>).
    /// That made <c>Path.Combine("", "Pix2d")</c> a relative "Pix2d" which <c>CreateDirectory</c> then tried
    /// to create next to the executable (itself named "Pix2d") and crashed on a fresh profile. Passing
    /// <see cref="Environment.SpecialFolderOption.Create"/> materialises and returns the real XDG dir; the
    /// <c>$HOME/.local/share</c> fallback covers the (unusual) case where it is still empty.
    /// </summary>
    private static string GetLocalApplicationDataPath()
    {
        var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create);

        if (!string.IsNullOrWhiteSpace(path))
            return path;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home) ? "" : Path.Combine(home, ".local", "share");
    }

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

        void LogError(string message, Exception? ex = null)
        {
            Console.WriteLine($"ERROR: {message} - {ex?.Message}");
            if (ex != null) Console.WriteLine(ex.ToString());
        }
    }
}