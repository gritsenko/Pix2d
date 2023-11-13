using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes.Interactive;

namespace Pix2d.Desktop.Services;
public class PlatformStuffService : IPlatformStuffService
{
    public PlatformStuffService(AppState state)
    {
        state.PropertyChanged += (_, p) => { if (p.PropertyName == nameof(state.WindowTitle)) SetWindowTitle(state.WindowTitle); };
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


    public DeviceFormFactorType GetDeviceFormFactorType()
    {
        throw new NotImplementedException();
    }

    public void SetWindowTitle(string title)
    {
        if (EditorApp.TopLevel is MainWindow wnd) {
            Dispatcher.UIThread.Post(() =>
            {
                wnd.Title = title + " - Pix2d v" + GetAppVersion();
            });
        }
    }

    public MemoryInfo GetMemoryInfo()
    {
        var proc = Process.GetCurrentProcess();
        var mem = proc.PrivateMemorySize64;
        var available = Environment.WorkingSet;
        return new MemoryInfo((ulong)available, (ulong)mem);
    }


    public bool Is64App => Environment.Is64BitOperatingSystem;

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
        catch(Exception ex)
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
}