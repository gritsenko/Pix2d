using System;
using System.IO;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;

namespace Pix2d.iOS.Services
{
    public class IosPlatformStuffService : IPlatformStuffService
    {
        private string _appVersion;

        public bool Is64App { get; set; }
        public string KeyToString(VirtualKeys key)
        {
            return key.ToString();
        }

        public string GetAppVersion()
        {
            var info = new {VersionString = 0, BuildString = 0};
            _appVersion ??= $"{info.VersionString} ({info.BuildString})s";
            return _appVersion;
        }

        public void ToggleTopmostWindow()
        {
            throw new NotImplementedException();
        }

        public bool HasKeyboard { get; }
        public bool CanShare { get; }
        public void Share(IStreamExporter exporter, double scale = 1)
        {
            throw new NotImplementedException();
        }

        public void ToggleFullscreenMode()
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ShareImage(Stream bitmapImageStream)
        {
            return false;
        }

        public IosPlatformStuffService()
        {
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if(arch != null)
                Is64App = arch.Contains("64");
        }

        public async void OpenUrlInBrowser(string url)
        {
            var uri = new Uri(url);

            //var success = await Windows.System.Launcher.LaunchUriAsync(uri);
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
            //todo: change to ios memory usage
            return new MemoryInfo(0, 0);
        }
    }
}