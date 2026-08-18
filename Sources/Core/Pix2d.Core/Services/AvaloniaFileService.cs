#nullable enable
using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Infrastructure;
using Pix2d.Messages;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class AvaloniaFileService(
    IMessenger messenger,
    IPlatformStuffService platformStuffService,
    ISettingsService settingsService,
    IDialogService dialogService) : IFileService
{
    private readonly SemaphoreSlim _openDialogSemaphore = new(1, 1);

    /// <summary>
    /// Runs a platform file/folder picker and converts a failure of the dialog *itself* into the caller's
    /// "no selection" value instead of an exception. The Win32 shell dialog can fail outright — a broken
    /// shell extension, a denied COM activation or a torn-down parent window all surface as
    /// <c>Win32Exception: Unspecified error</c> — and that used to travel all the way up to
    /// <c>ExportService</c>, which has no case for it and told the user "There's nothing to Export!".
    /// Every picker in this service goes through here so the message is right and stated once, no matter
    /// which caller (export, palette, save project, localization) opened the dialog.
    /// </summary>
    private async Task<T> RunPickerGuardedAsync<T>(Func<Task<T>> showPicker, T onDialogFailure, T onCancelled)
    {
        // prevent from parallel opening several dialogs
        await _openDialogSemaphore.WaitAsync();
        try
        {
            return await showPicker();
        }
        catch (OperationCanceledException)
        {
            return onCancelled;
        }
        catch (Exception e)
        {
            Logger.LogException(e);
            dialogService.Alert(
                "Couldn't open the system file dialog. This is usually a temporary Windows shell problem — "
                + "try again, and restart Pix2d if it keeps happening.",
                "Files");
            return onDialogFailure;
        }
        finally
        {
            _openDialogSemaphore.Release();
        }
    }

    public virtual Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter,
        bool allowMultiplyFiles = false, string? contextKey = null)
        => RunPickerGuardedAsync<IEnumerable<IFileContentSource>>(async () =>
        {
            var sp = GetStorageProvider();

            var options = new FilePickerOpenOptions
            {
                AllowMultiple = allowMultiplyFiles,
                FileTypeFilter =
                [
                    new FilePickerFileType("Pix2d supported images")
                        { Patterns = fileTypeFilter.Select(x => "*" + x).ToArray() }
                ]
            };

            options.SuggestedStartLocation = await GetStartLocationAsync(sp, contextKey);

            var result = await sp.OpenFilePickerAsync(options);

            RememberFolder(contextKey, result.FirstOrDefault());

            return result.Select(GetFileSource);
        }, onDialogFailure: [], onCancelled: []);

    private IStorageProvider GetStorageProvider()
    {
        return EditorApp.TopLevel?.StorageProvider ?? throw new InvalidOperationException("StorageProvider is null");
    }

    /// <summary>
    /// Resolves the folder a picker should open in for the given context, implementing the per-context
    /// last-folder memory documented on <see cref="IFileService"/>. Without an explicit start location the
    /// Win32 picker falls back to the process working directory, which is C:\Windows\System32 when Pix2d is
    /// launched through a file association — users then accepted that folder and every write failed with
    /// UnauthorizedAccessException. Falls back to Documents so the default is always writable.
    /// </summary>
    private async Task<IStorageFolder?> GetStartLocationAsync(IStorageProvider sp, string? contextKey)
    {
        try
        {
            if (GetContextFolders().TryGetValue(contextKey ?? DefaultContextKey, out var lastPath)
                && !string.IsNullOrWhiteSpace(lastPath)
                && Directory.Exists(lastPath))
            {
                var folder = await sp.TryGetFolderFromPathAsync(lastPath);
                if (folder != null)
                    return folder;
            }

            return await sp.TryGetWellKnownFolderAsync(WellKnownFolder.Documents);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return null;
        }
    }

    private const string DefaultContextKey = "default";

    private Dictionary<string, string> GetContextFolders()
    {
        try
        {
            return settingsService.Get<Dictionary<string, string>>(SettingsConstants.FileServiceContexts) ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return [];
        }
    }

    private void RememberFolder(string? contextKey, IStorageItem? item)
    {
        try
        {
            var path = item?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
                return;

            // A picked file gives us its folder; a picked folder is already the folder we want to reuse.
            var folderPath = item is IStorageFolder ? path : Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(folderPath))
                return;

            var contexts = GetContextFolders();
            contexts[contextKey ?? DefaultContextKey] = folderPath;
            settingsService.Set(SettingsConstants.FileServiceContexts, contexts);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public async Task<bool> SaveTextToFileWithDialogAsync(string text, string[] fileTypeFilter,
        string? contextKey = null, string? defaultFileName = null)
    {
        var filePickResult = await GetFileToSaveWithDialogAsync(fileTypeFilter, contextKey, defaultFileName);

        return await filePickResult.MatchAsync(async f =>
        {
            await f.SaveAsync(text);
            return true;
        }, _ => Task.FromResult(false));
    }

    public async Task<bool> SaveStreamToFileWithDialogAsync(Func<Task<Stream>> sourceStreamProvider,
        string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null)
    {
        var filePickResult = await GetFileToSaveWithDialogAsync(fileTypeFilter, contextKey, defaultFileName);

        return await filePickResult.MatchAsync(async f =>
        {
            // Produce the payload *first*. This used to open the destination before awaiting the provider,
            // so an exporter that threw while encoding left the file already truncated — the user asked to
            // overwrite a good PNG/GIF/SVG and ended up with nothing.
            await using var sourceStream = await sourceStreamProvider();
            await f.SaveAsync(sourceStream);
            return true;
        }, _ => Task.FromResult(false));
    }

    public Task<Result<IFileContentSource, FileDialogResultError>> GetFileToSaveWithDialogAsync(
        string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null)
        => RunPickerGuardedAsync<Result<IFileContentSource, FileDialogResultError>>(async () =>
        {
            var options = new FilePickerSaveOptions();
            var extension = string.Join(", ", fileTypeFilter);
            options.FileTypeChoices =
            [
                new FilePickerFileType(extension) { Patterns = fileTypeFilter.Select(x => "*" + x).ToArray() },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ];

            if (fileTypeFilter.Length > 0)
            {
                options.DefaultExtension = fileTypeFilter.First().Trim('.');
            }

            options.SuggestedFileName = defaultFileName ?? "untitled";

            var sp = GetStorageProvider();

            options.SuggestedStartLocation = await GetStartLocationAsync(sp, contextKey);

            var result = await sp.SaveFilePickerAsync(options);
            if (result == null)
            {
                return FileDialogResultError.NoFileSelected;
            }

            RememberFolder(contextKey, result);

            return Result<IFileContentSource, FileDialogResultError>.FromNullable(GetFileSource(result),
                FileDialogResultError.FileSourceNotCreated);
        }, onDialogFailure: FileDialogResultError.DialogFailed,
           onCancelled: FileDialogResultError.NoFileSelected);

    protected virtual IFileContentSource GetFileSource(IStorageFile file)
    {
        return new AvaloniaFileSource(file);
    }

    public Task<IWriteDestinationFolder?> GetFolderToExportWithDialogAsync(string? contextKey = null)
        => RunPickerGuardedAsync<IWriteDestinationFolder?>(async () =>
        {
            var sp = GetStorageProvider();

            var options = new FolderPickerOpenOptions() { Title = "Select folder to export" };
            options.SuggestedStartLocation = await GetStartLocationAsync(sp, contextKey);

            var folders = await sp.OpenFolderPickerAsync(options);
            var folder = folders.FirstOrDefault();

            RememberFolder(contextKey, folder);

            return folder != null ? new AvaloniaFolder(folder) : null;
        }, onDialogFailure: null, onCancelled: null);

    public Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false)
    {
        var path = Path.Combine(platformStuffService.GetAppFolderPath(), name);
        if (deleteIfExist && Directory.Exists(path))
            Directory.Delete(path, true);
        return Task.FromResult<IWriteDestinationFolder>(new NetFolder(path));
    }

    public Task<IFileContentSource> GetFileContentSourceAsync(string fileName)
    {
        return Task.FromResult<IFileContentSource>(new NetFileSource(fileName));
    }

    /// <summary>
    /// Records a file as most-recently-used. Matching is by <see cref="MruRecord.GetComparisonKey"/>, not by
    /// raw path: the same file arriving under a different spelling (forward slashes, different case on
    /// Windows) used to be stored a second time and the recent-projects list then showed one project twice.
    /// An already-known file is promoted to the front rather than skipped, and a file that is already at the
    /// front under the same name is left alone — otherwise every Ctrl+S would rewrite the settings and make
    /// the recents view reload all its thumbnails.
    /// </summary>
    public void AddToMru(IFileContentSource fileSource)
    {
        try
        {
            var mruList = settingsService.Get<List<MruRecord>>("mru") ?? [];
            var newRecord = new MruRecord(fileSource);
            var key = newRecord.GetComparisonKey();

            if (mruList.Count > 0
                && MruRecord.NormalizePath(mruList[0].Path) == key
                && mruList[0].Name == newRecord.Name)
                return;

            mruList.RemoveAll(x => MruRecord.NormalizePath(x.Path) == key);
            mruList.Insert(0, newRecord);
            settingsService.Set("mru", mruList);
            messenger.Send(new MruChangedMessage());
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    /// <summary>
    /// Reads the MRU list, dropping unusable and duplicate entries. The de-duplication also repairs lists
    /// polluted before <see cref="AddToMru"/> compared normalized paths — a list that had to be cleaned is
    /// written back once, so the junk stops being carried forward. No <see cref="MruChangedMessage"/> is
    /// sent: this runs *while* the recents view is building itself from the result.
    /// </summary>
    public Task<List<IFileContentSource>> GetMruFilesAsync()
    {
        try
        {
            var mruList = settingsService.Get<List<MruRecord>>("mru");
            if (mruList == null)
                return Task.FromResult(new List<IFileContentSource>());

            var seen = new HashSet<string>();
            var records = new List<MruRecord>(mruList.Count);
            var changed = false;
            foreach (var record in mruList)
            {
                if (string.IsNullOrWhiteSpace(record.Path) || string.IsNullOrWhiteSpace(record.Name))
                {
                    changed = true;
                    continue;
                }

                if (!seen.Add(record.GetComparisonKey()))
                {
                    changed = true;
                    continue;
                }

                // Repair the stored spelling too, not just the comparison: a record written before this
                // (a dropped file, so "C:/Users/…/art.pix2d") would otherwise keep showing its
                // forward-slash path in the recent-projects tooltip.
                var canonical = MruRecord.CanonicalizePath(record.Path);
                if (canonical != record.Path)
                {
                    record.Path = canonical;
                    changed = true;
                }

                records.Add(record);
            }

            if (changed)
                settingsService.Set("mru", records);

            return Task.FromResult(records
                .Select(IFileContentSource (x) => new NetFileSource(x.Path)
                {
                    Title = x.Name
                }).ToList());
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        return Task.FromResult(new List<IFileContentSource>());
    }

    public void RemoveFromMru(string sourcePath)
    {
        try
        {
            var mruList = settingsService.Get<List<MruRecord>>("mru") ?? [];
            var key = MruRecord.NormalizePath(sourcePath);
            mruList.RemoveAll(x => MruRecord.NormalizePath(x.Path) == key);
            settingsService.Set("mru", mruList);

            messenger.Send(new MruChangedMessage());
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }
}