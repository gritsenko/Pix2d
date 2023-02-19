using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Editor.Desktop.Services;
public class PlatformStuffService : IPlatformStuffService
{
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
        var str = key.ToString();
        return str;
    }

    public string GetAppVersion()
    {
        throw new NotImplementedException();
    }

    public Task<bool> ShareImage(Stream bitmapImageStream)
    {
        throw new NotImplementedException();
    }

}