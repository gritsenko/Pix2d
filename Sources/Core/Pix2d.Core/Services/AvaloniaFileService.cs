#nullable enable
using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
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

            var result = await sp.OpenFilePickerAsync(options);

            return result.Select(GetFileSource);
        }
        finally
        {
            _openDialogSemaphore.Release();
        }
    }

    private IStorageProvider GetStorageProvider()
    {
        return EditorApp.TopLevel.StorageProvider;
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

            var result = await sp.SaveFilePickerAsync(options);
            if (result == null)
            {
                return FileDialogResultError.NoFileSelected;
            }

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
            var folders = await sp.OpenFolderPickerAsync(options);
            var folder = folders.FirstOrDefault();

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
            var mruList = settingsService.Get<HashSet<MruRecord>>("mru")?.ToHashSet() ?? [];
            mruList.Add(new MruRecord(fileSource));
            settingsService.Set("mru", mruList);

            messenger.Send(new MruChangedMessage());
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
            var mruList = settingsService.Get<HashSet<MruRecord>>("mru")?.ToHashSet();

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
            var mruList = settingsService.Get<HashSet<MruRecord>>("mru")?.ToHashSet() ?? [];
            mruList.RemoveWhere(x => x.Path == sourcePath);
            settingsService.Set("mru", mruList);

            messenger.Send(new MruChangedMessage());
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }
}