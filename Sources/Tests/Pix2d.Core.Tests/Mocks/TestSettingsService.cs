using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Core.Tests.Mocks;

public class TestSettingsService : ISettingsService
{
    public T? Get<T>(string key)
    {
        return default;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        value = default;
        return false;
    }

    public void Set<T>(string key, T? value)
    {
    }
}

public class TestPlatformStaffService : IPlatformStuffService
{
    public void OpenUrlInBrowser(string url)
    {
    }

    public DeviceFormFactorType GetDeviceFormFactorType()
    {
        throw new NotImplementedException();
    }

    public void SetWindowTitle(string title)
    {
        throw new NotImplementedException();
    }

    public MemoryInfo GetMemoryInfo()
    {
        throw new NotImplementedException();
    }

    public bool Is64App { get; }
    public string KeyToString(VirtualKeys key)
    {
        throw new NotImplementedException();
    }

    public string GetAppVersion()
    {
        throw new NotImplementedException();
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
}