using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Pix2d.Services
{
    public class AndroidPlatformStuffService : IPlatformStuffService
    {
        private DeviceFormFactorType _deviceFormFactorType = DeviceFormFactorType.Desktop;
        private string _appVersion;

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

        public async void OpenUrlInBrowser(string url)
        {
            var uri = new Uri(url);

            //var success = await Windows.System.Launcher.LaunchUriAsync(uri);
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
            //string GetAppVer()
            //{
            //    var package = Package.Current;
            //    var packageId = package.Id;
            //    var version = packageId.Version;

            //    return $"{version.Major}.{version.Minor}";
            //}

            //return _appVersion ?? (_appVersion = GetAppVer());
            return "Alpha";
        }

        public Task<bool> ShareImage(Stream bitmapImageStream)
        {
            throw new NotImplementedException();
        }

        public void ToggleTopmostWindow()
        {
            throw new NotImplementedException();
        }

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

    }
}