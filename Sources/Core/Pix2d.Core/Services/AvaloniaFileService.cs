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
    ISettingsService settingsService) : IFileService
{
    private readonly SemaphoreSlim _openDialogSemaphore = new(1, 1);

    public virtual async Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter,
        bool allowMultiplyFiles = false, string? contextKey = null)
    {
        // prevent from parallel opening several dialogs
        await _openDialogSemaphore.WaitAsync();

        try
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
        }
        finally
        {
            _openDialogSemaphore.Release();
        }
    }

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
            await using var fileStream = await f.OpenWriteAsync();
            await using var sourceStream = await sourceStreamProvider();
            await sourceStream.CopyToAsync(fileStream);
            await fileStream.FlushAsync();
            return true;
        }, _ => Task.FromResult(false));
    }

    public async Task<Result<IFileContentSource, FileDialogResultError>> GetFileToSaveWithDialogAsync(
        string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null)
    {
        try
        {
            await _openDialogSemaphore.WaitAsync();


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
        }
        finally
        {
            _openDialogSemaphore.Release();
        }
    }

    protected virtual IFileContentSource GetFileSource(IStorageFile file)
    {
        return new AvaloniaFileSource(file);
    }

    public async Task<IWriteDestinationFolder?> GetFolderToExportWithDialogAsync(string? contextKey = null)
    {
        try
        {
            await _openDialogSemaphore.WaitAsync();

            var sp = GetStorageProvider();

            var options = new FolderPickerOpenOptions() { Title = "Select folder to export" };
            options.SuggestedStartLocation = await GetStartLocationAsync(sp, contextKey);

            var folders = await sp.OpenFolderPickerAsync(options);
            var folder = folders.FirstOrDefault();

            RememberFolder(contextKey, folder);

            return folder != null ? new AvaloniaFolder(folder) : null;
        }
        finally
        {
            _openDialogSemaphore.Release();
        }
    }

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

    public void AddToMru(IFileContentSource fileSource)
    {
        try
        {
            var mruList = settingsService.Get<List<MruRecord>>("mru") ?? [];
            var newRecord = new MruRecord(fileSource);
            if (!mruList.Any(x => x.Path == newRecord.Path))
            {
                mruList.Insert(0, newRecord);
                settingsService.Set("mru", mruList);
                messenger.Send(new MruChangedMessage());
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public Task<List<IFileContentSource>> GetMruFilesAsync()
    {
        try
        {
            var mruList = settingsService.Get<List<MruRecord>>("mru");

            return Task.FromResult(mruList?
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Name))
                .Select(IFileContentSource (x) => new NetFileSource(x.Path)
                {
                    Title = x.Name
                }).ToList() ?? []);
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
            mruList.RemoveAll(x => x.Path == sourcePath);
            settingsService.Set("mru", mruList);

            messenger.Send(new MruChangedMessage());
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }
}