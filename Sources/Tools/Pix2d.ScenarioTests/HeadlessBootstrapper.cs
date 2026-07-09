using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;
using Pix2d.Infrastructure.Logger;
using Pix2d.Plugins.Drawing;
using Pix2d.Plugins.ImageFormats.PngFormat;
using Pix2d.Plugins.Sprite;
using Pix2d.Services;
using SkiaNodes.Interactive;

namespace Pix2d.ScenarioTests;

/// <summary>
/// Minimal headless bootstrapper for the scenario harness. Reuses the real
/// <see cref="Pix2dBootstrapperDI"/> service graph, but:
///   - loads only Sprite + Drawing + Png plugins (no AI / OpenCv / PixelText heaviness),
///   - registers a no-op <see cref="IPlatformStuffService"/> with SupportsMultipleProjects = false
///     (keeps the simple single-project replace path — no tabs, no session recovery),
///   - uses the in-process <see cref="InternalClipboardService"/> (the head normally supplies a
///     platform clipboard),
///   - points the app data folder at a throwaway temp dir so settings / crash markers don't touch
///     the developer's real Pix2d profile.
/// The four Avalonia-flavoured base services (font / dialog / file / ui-scale) are construct-safe;
/// they only touch Avalonia on method use, which the boot + draw path never triggers.
/// </summary>
public sealed class HeadlessBootstrapper : Pix2dBootstrapperDI
{
    private readonly string _appFolder;

    public HeadlessBootstrapper()
    {
        _appFolder = Path.Combine(Path.GetTempPath(), "Pix2d.ScenarioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_appFolder);
    }

    protected override Pix2DAppSettings GetPix2dSettings() => new()
    {
        AppMode = Pix2DAppMode.SpriteEditor
    };

    public override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // Last-registration-wins: these override / fill in what a real head would supply.
        services.AddSingleton<IPlatformStuffService>(new HeadlessPlatformStuffService(_appFolder));
        services.AddSingleton<IClipboardService, InternalClipboardService>();
        // Replace the Avalonia dialog service: a swept command that pops a dialog would otherwise try
        // to build an Avalonia view with no TopLevel. All calls become deterministic no-ops.
        services.AddSingleton<IDialogService, HeadlessDialogService>();
    }

    protected override void LoadPlugins()
    {
        // Deliberately NOT base.LoadPlugins() — we want a lean, deterministic plugin set.
        LoadPlugin<SpritePlugin>();
        LoadPlugin<DrawingPlugin>();
        LoadPlugin<PngFormatPlugin>();
    }

    protected override bool InitTelemetry()
    {
        Logger.RegisterLoggerTarget(new ConsoleLoggerTarget());
        return true;
    }
}

/// <summary>No-op platform surface for headless runs. Nothing here opens a window or touches the OS
/// beyond returning a temp app-data folder.</summary>
internal sealed class HeadlessPlatformStuffService(string appFolder) : IPlatformStuffService
{
    // Default interface members give SupportsMultipleProjects = false and SupportsSelfUpdate = false,
    // which is exactly what a single-project headless harness wants.
    public PlatformType CurrentPlatform => PlatformType.CrossPlatformDesktop;
    public bool IsTextInputFocused => false;
    public bool HasKeyboard => true;
    public bool CanShare => false;

    public void OpenUrlInBrowser(string url) { }
    public void SetWindowTitle(string title) { }
    public MemoryInfo GetMemoryInfo() => new(0, 0);
    public string KeyToString(VirtualKeys key) => key.ToString();
    public string GetAppVersion() => "0.0.0-headless";
    public void ToggleTopmostWindow() { }
    public void Share(IStreamExporter exporter, double scale = 1) { }
    public void ToggleFullscreenMode() { }
    public string GetAppFolderPath() => appFolder;
    public Task OpenAppDataFolder() => Task.CompletedTask;
}

/// <summary>No-op dialog surface: every prompt resolves to a safe default and no Avalonia view is built.</summary>
internal sealed class HeadlessDialogService : IDialogService
{
    public void SetDialogContainer(object container) { }
    public void SetPanelsContainer(object container) { }
    public void Alert(string message, string title) { }
    public Task ShowAlert(string message, string title) => Task.CompletedTask;
    public Task<string?> ShowInputDialogAsync(string message, string title, string defaultValue = "")
        => Task.FromResult<string?>(null);
    public Task<bool> ShowYesNoDialog(string message, string title, string okLabel = "Ok", string cancelLabel = "Cancel")
        => Task.FromResult(false);
    public Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog()
        => Task.FromResult(UnsavedChangesDialogResult.No);
    public void ShowPanelView(IToolPanel panel) { }
    public void TogglePanelView(IToolPanel panel) { }
    public Task<TResult> ShowDialogAsync<TResult>(IDialogView<TResult> dialog) => Task.FromResult<TResult>(default!);
}
