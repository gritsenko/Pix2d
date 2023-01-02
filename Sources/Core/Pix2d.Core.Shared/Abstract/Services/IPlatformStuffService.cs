using System.IO;
using System.Threading.Tasks;
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

        Task<bool> ShareImage(Stream bitmapImageStream);
    }

    public enum DeviceFormFactorType
    {
        Phone,
        Tablet,
        Desktop
    }
}