using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;
using System;
using System.IO;
using System.Linq;
using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using Pix2d.Abstract.Export;
using Pix2d.Android;
using File = Java.IO.File;
using Microsoft.Maui.ApplicationModel;

namespace Pix2d.Services;

public class AndroidPlatformStuffService : IPlatformStuffService
{
    private DeviceFormFactorType _deviceFormFactorType = DeviceFormFactorType.Desktop;
    private string? _appVersion;

    public AndroidPlatformStuffService()
    {
        var diag = GetScreenDiagonal();
        if (diag < 7)
        {
            _deviceFormFactorType = DeviceFormFactorType.Phone;
        }
        else if (diag < 12)
        {
            _deviceFormFactorType = DeviceFormFactorType.Tablet;
        }

    }

    public void OpenUrlInBrowser(string url)
    {
        var uri = global::Android.Net.Uri.Parse(url);
        var intent = new Intent(Intent.ActionView, uri);
        MainActivity.Instance.StartActivity(intent);
    }

    public DeviceFormFactorType GetDeviceFormFactorType()
    {
        return _deviceFormFactorType;
    }

    public void SetWindowTitle(string title)
    {

    }

    public MemoryInfo GetMemoryInfo()
    {
        return new MemoryInfo(0, 0);
    }

    public bool Is64App { get; }
    public string KeyToString(VirtualKeys key)
    {
        return key.ToString();
    }

    public string GetAppVersion()
    {
        var info = AppInfo.Current;
        _appVersion ??= $"{info.VersionString} ({info.BuildString})s";
        return _appVersion;
    }

    public void ToggleTopmostWindow()
    {
        throw new NotImplementedException();
    }

    public bool HasKeyboard => false;
    public bool CanShare => true;

    public static double GetScreenDiagonal()
    {
        //try
        //{
        //    var display = ((Activity)MainActivity.Instance)?.WindowManager?.DefaultDisplay;
        //    if (display == null)
        //        return -1;

        //    var displayMetrics = new DisplayMetrics();
        //    display.GetMetrics(displayMetrics);

        //    if (displayMetrics == null)
        //        return -1;

        //    var wInches = displayMetrics.WidthPixels / (double)displayMetrics.DensityDpi;
        //    var hInches = displayMetrics.HeightPixels / (double)displayMetrics.DensityDpi;

        //    var screenDiagonal = Math.Sqrt(Math.Pow(wInches, 2) + Math.Pow(hInches, 2));
        //    return screenDiagonal;
        //}
        //catch (Exception e)
        //{
        //    Logger.LogException(e);
        //}
            
        return -1;
    }

    public async void Share(IStreamExporter exporter, double scale)
    {
        var tempFilename = "pix2d_share" + exporter.SupportedExtensions.First();
        var sdCardPath = Path.Combine(Application.Context.ExternalCacheDir.AbsolutePath, "tmp");
        if (!Directory.Exists(sdCardPath))
        {
            Directory.CreateDirectory(sdCardPath);
        }
            
        var filePath = Path.Combine(sdCardPath, tempFilename);
        using (var os = new FileStream(filePath, FileMode.Create))
        {
            var nodes = CoreServices.ExportService.GetNodesToExport(scale);
            var source = await exporter.ExportToStreamAsync(nodes, scale);
            await source.CopyToAsync(os);
            os.Close();
        }

        var imageUri = FileProvider.GetUriForFile(Application.Context, Application.Context.PackageName + ".fileprovider",
            new File(filePath));
        var sharingIntent = new Intent ();
        sharingIntent.SetAction (Intent.ActionSend);
        sharingIntent.SetType(exporter.MimeType);
        sharingIntent.PutExtra (Intent.ExtraStream, imageUri);
        sharingIntent.AddFlags (ActivityFlags.GrantReadUriPermission);
        MainActivity.Instance.StartActivity (Intent.CreateChooser (sharingIntent, "Pix2d project"));
    }

    public void ToggleFullscreenMode()
    {
    }
}