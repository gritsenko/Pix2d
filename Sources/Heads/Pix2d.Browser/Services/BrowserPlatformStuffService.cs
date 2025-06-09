using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes.Interactive;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Pix2d.Browser.Services;

public class BrowserPlatformStuffService : IPlatformStuffService
{
    private string _appVersion;

    public bool IsTextInputFocused => EditorApp.TopLevel.FocusManager?.GetFocusedElement() is TextBox;

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

    public void ToggleTopmostWindow()
    {

    }

    public bool HasKeyboard => true;
    public bool CanShare => false;
    public void Share(IStreamExporter exporter, double scale = 1)
    {
        throw new NotSupportedException();
    }

    public void ToggleFullscreenMode()
    {
    }

    public string GetAppFolderPath() => "/";
    public Task OpenAppDataFolder()
    {
        throw new NotImplementedException();
    }

    public BrowserPlatformStuffService(AppState state)
    {
        state.PropertyChanged += (_, p) => { if (p.PropertyName == nameof(state.WindowTitle)) SetWindowTitle(state.WindowTitle); };
    }

    public PlatformType CurrentPlatform => PlatformType.WASM;

    public async void OpenUrlInBrowser(string url)
    {
        var uri = new Uri(url);
        throw new NotImplementedException();
        //var success = await Windows.System.Launcher.LaunchUriAsync(uri);
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