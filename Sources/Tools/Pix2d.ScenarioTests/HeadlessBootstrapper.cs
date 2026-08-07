using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;
using Pix2d.Common.FileSystem;
using Pix2d.Infrastructure;
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
///     the developer's real Pix2d profile,
///   - answers every file/folder picker from a temp folder via <see cref="HeadlessFileService"/>, so the
///     export destination logic (Save dialog vs. folder) is exercisable without a TopLevel.
/// The remaining Avalonia-flavoured base services (font / ui-scale) are construct-safe; they only touch
/// Avalonia on method use, which the boot + draw path never triggers.
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
        // Replace the Avalonia file service: every picker needs a TopLevel. This one answers each picker
        // from a temp folder and records what was asked for, which is what makes export destinations and
        // suggested file names assertable headlessly.
        services.AddSingleton<IFileService>(new HeadlessFileService(Path.Combine(_appFolder, "pickers")));
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

/// <summary>No-op dialog surface: every prompt resolves to a safe default and no Avalonia view is built.
/// Yes/No prompts are scriptable so a scenario can drive a confirmation flow.</summary>
/// <summary>
/// Stands in for the Avalonia file service. Every picker resolves against one throwaway folder, and what
/// the caller asked for is recorded — <see cref="LastSuggestedFileName"/> is how a scenario asserts that an
/// export suggests the artboard's name instead of the old hardcoded "untitled".
/// </summary>
public sealed class HeadlessFileService(string rootPath) : IFileService
{
    /// <summary>Folder every picker resolves to.</summary>
    public string RootPath { get; } = rootPath;

    /// <summary>Suggested name passed to the most recent save picker (null when the caller passed none).</summary>
    public string? LastSuggestedFileName { get; private set; }

    /// <summary>Set false to make the next save/folder picker behave as if the user cancelled.</summary>
    public bool PickerSucceeds { get; set; } = true;

    /// <summary>Number of times a folder picker was shown — a batch export must only ever ask once.</summary>
    public int FolderPickerCalls { get; private set; }

    private IWriteDestinationFolder Root()
    {
        Directory.CreateDirectory(RootPath);
        return new NetFolder(RootPath);
    }

    public Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter,
        bool allowMultiplyFiles = false, string? contextKey = null)
        => Task.FromResult(Enumerable.Empty<IFileContentSource>());

    public Task<Result<IFileContentSource, FileDialogResultError>> GetFileToSaveWithDialogAsync(
        string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null)
    {
        LastSuggestedFileName = defaultFileName;
        if (!PickerSucceeds)
            return Task.FromResult<Result<IFileContentSource, FileDialogResultError>>(FileDialogResultError.NoFileSelected);

        var ext = fileTypeFilter.FirstOrDefault() ?? ".png";
        var file = Root().GetFileSource(defaultFileName ?? "untitled", ext, true);
        return Task.FromResult(Result<IFileContentSource, FileDialogResultError>.FromNullable(
            file, FileDialogResultError.FileSourceNotCreated));
    }

    public async Task<bool> SaveTextToFileWithDialogAsync(string text, string[] fileTypeFilter,
        string? contextKey = null, string? defaultFileName = null)
    {
        LastSuggestedFileName = defaultFileName;
        if (!PickerSucceeds)
            return false;

        var file = Root().GetFileSource(defaultFileName ?? "untitled", fileTypeFilter.FirstOrDefault() ?? ".txt", true);
        await file.SaveAsync(text);
        return true;
    }

    public async Task<bool> SaveStreamToFileWithDialogAsync(Func<Task<Stream>> streamProvider,
        string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null)
    {
        LastSuggestedFileName = defaultFileName;
        if (!PickerSucceeds)
            return false;

        var file = Root().GetFileSource(defaultFileName ?? "untitled", fileTypeFilter.FirstOrDefault() ?? ".png", true);
        await using var stream = await streamProvider();
        await file.SaveAsync(stream);
        return true;
    }

    public Task<IWriteDestinationFolder?> GetFolderToExportWithDialogAsync(string? contextKey = null)
    {
        FolderPickerCalls++;
        return Task.FromResult(PickerSucceeds ? Root() : null);
    }

    public Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false)
    {
        var path = Path.Combine(RootPath, name);
        if (deleteIfExist && Directory.Exists(path))
            Directory.Delete(path, true);
        Directory.CreateDirectory(path);
        return Task.FromResult<IWriteDestinationFolder>(new NetFolder(path));
    }

    public Task<IFileContentSource> GetFileContentSourceAsync(string fileName)
        => Task.FromResult<IFileContentSource>(new NetFileSource(fileName));

    public void AddToMru(IFileContentSource fileSource) { }
    public Task<List<IFileContentSource>> GetMruFilesAsync() => Task.FromResult(new List<IFileContentSource>());
    public void RemoveFromMru(string sourcePath) { }
}

public sealed class HeadlessDialogService : IDialogService
{
    /// <summary>Answer returned by the next <see cref="ShowYesNoDialog"/>. Defaults to <c>false</c> so the
    /// command sweep declines destructive prompts; a scenario flips it to exercise the confirmed path.</summary>
    public bool YesNoAnswer { get; set; }

    /// <summary>Message of the most recent Yes/No prompt, so a scenario can assert what the user was asked.</summary>
    public string? LastYesNoMessage { get; private set; }

    /// <summary>Text returned by the next <see cref="ShowInputDialogAsync"/>. Null (the default) means the
    /// user dismissed the prompt, so the sweep never renames anything by accident.</summary>
    public string? InputAnswer { get; set; }

    /// <summary>Default value the most recent input prompt was seeded with.</summary>
    public string? LastInputDefaultValue { get; private set; }

    public void SetDialogContainer(object container) { }
    public void SetPanelsContainer(object container) { }
    public void Alert(string message, string title) { }
    public Task ShowAlert(string message, string title) => Task.CompletedTask;
    public Task<string?> ShowInputDialogAsync(string message, string title, string defaultValue = "")
    {
        LastInputDefaultValue = defaultValue;
        return Task.FromResult(InputAnswer);
    }
    public Task<bool> ShowYesNoDialog(string message, string title, string okLabel = "Ok", string cancelLabel = "Cancel")
    {
        LastYesNoMessage = message;
        return Task.FromResult(YesNoAnswer);
    }
    public Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog()
        => Task.FromResult(UnsavedChangesDialogResult.No);
    public void ShowPanelView(IToolPanel panel) { }
    public void TogglePanelView(IToolPanel panel) { }
    public Task<TResult> ShowDialogAsync<TResult>(IDialogView<TResult> dialog) => Task.FromResult<TResult>(default!);
}
