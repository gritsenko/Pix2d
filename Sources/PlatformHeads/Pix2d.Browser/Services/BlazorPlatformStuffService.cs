using System;
using System.IO;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Messages;
using SkiaNodes.Interactive;

namespace Pix2d.Browser.Services
{
    public class BlazorPlatformStuffService : IPlatformStuffService
    {
//        private ApplicationView _appView;
        private string _appVersion;

        public bool Is64App { get; set; }
        public string KeyToString(VirtualKeys key)
        {
            return key.ToString();
        }

        public string GetAppVersion()
        {
            string GetAppVer()
            {
                return $"0.0";
            }

            return _appVersion ??= GetAppVer();
        }

        public async Task<bool> ShareImage(Stream bitmapImageStream)
        {
            return false;
        }

        public void ToggleTopmostWindow()
        {
            
        }

        public bool HasKeyboard => true;

        public BlazorPlatformStuffService(IMessenger messenger)
        {
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
            if(arch != null)
                Is64App = arch.Contains("64");

            messenger.Register<ProjectNameChangedMessage>(this, OnProjectNameChanged);
        }

        private void OnProjectNameChanged(ProjectNameChangedMessage message)
        {
            SetWindowTitle(message.NewName);
        }

        public async void OpenUrlInBrowser(string url)
        {
            var uri = new Uri(url);
            throw new NotImplementedException();
            //var success = await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        public DeviceFormFactorType GetDeviceFormFactorType()
        {
            throw new NotImplementedException();
        }

        public void SetWindowTitle(string title)
        {
            try
            {
                //if (_appView == null)
                //{
                //    _appView = ApplicationView.GetForCurrentView();
                //}

                //_appView.Title = title + " - v" + GetAppVersion();
            }
            catch (Exception ex)
            {
                //whatever!
            }
        }

        public MemoryInfo GetMemoryInfo()
        {
            return new MemoryInfo(1073741824, 0);
        }
    }
}