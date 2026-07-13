using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes.Interactive;
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Pix2d.Browser.Services;

internal static partial class BrowserInterop
{
    [JSImport("setTitle", "main.js")]
    internal static partial void SetTitle(string title);

    [JSImport("openUrl", "main.js")]
    internal static partial void OpenUrl(string url);
}

public class BrowserPlatformStuffService : IPlatformStuffService
{
    private string? _appVersion;

    public bool IsTextInputFocused => EditorApp.TopLevel?.FocusManager?.GetFocusedElement() is TextBox;

    public string KeyToString(VirtualKeys key)
    {
        return key.ToString();
    }

    public string GetAppVersion() => $"{Pix2d.Common.BuildInfo.Version} Web";

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
        // No user-visible file system in the browser sandbox.
        return Task.CompletedTask;
    }

    public BrowserPlatformStuffService(AppState state)
    {
        state.PropertyChanged += (_, p) => { if (p.PropertyName == nameof(state.WindowTitle)) SetWindowTitle(state.WindowTitle); };
    }

    public PlatformType CurrentPlatform => PlatformType.WASM;

    public void OpenUrlInBrowser(string url)
    {
        BrowserInterop.OpenUrl(url);
    }

    public void SetWindowTitle(string title)
    {
        try
        {
            BrowserInterop.SetTitle($"{title} - Pix2d v{GetAppVersion()}");
        }
        catch
        {
        }
    }

    public MemoryInfo GetMemoryInfo()
    {
        return new MemoryInfo(1073741824, 0);
    }
}