using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using SkiaNodes.Interactive;

namespace Pix2d.Abstract.Services
{
    public interface IPlatformStuffService
    {
        void OpenUrlInBrowser(string url);


        DeviceFormFactorType GetDeviceFormFactorType();

        void SetWindowTitle(string title);

        MemoryInfo GetMemoryInfo();

        bool Is64App { get; }

        string KeyToString(VirtualKeys key);

        string GetAppVersion();

        public void ToggleTopmostWindow();

        public bool HasKeyboard { get; }

        public bool CanShare { get; }
        public void Share(IStreamExporter exporter, double scale = 1);
        public void ToggleFullscreenMode();
    }

    public enum DeviceFormFactorType
    {
        Phone,
        Tablet,
        Desktop
    }
}