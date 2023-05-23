using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.Primitives;
using SkiaNodes.Serialization;

namespace Pix2d.Services;

public class AvaloiaFileService : IFileService {
    public IMessenger Messenger { get; }


    private ISettingsService SettingsService => CoreServices.SettingsService;

    public IFileContentSource FileOpenOnStartup { get; set; }
    public AvaloniaDialogService DialogService { get; }

    private bool _isDialogOpened;
    private readonly Dictionary<string, IDataStorage> _storages = new Dictionary<string, IDataStorage>();

    public AvaloiaFileService(IDialogService dialogService, IMessenger messenger)
    {
        Messenger = messenger;
        DialogService = dialogService as AvaloniaDialogService;
    }

    public virtual async Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false,
        string contextKey = null) {
        // проверяем и предотвращаем повтороное открытие диалога
        if (_isDialogOpened)
            return null;

        try {
            OnFileDialogOpened();

            var sp = GetStorageProvider();

            var options = new FilePickerOpenOptions();
            options.FileTypeFilter = new[]
            {
                new FilePickerFileType("Pix2d supported images") { Patterns = fileTypeFilter.Select(x=>"*" + x).ToArray() }
            };

            var result = await sp.OpenFilePickerAsync(options);
            if (result == null || !result.Any()) {
                return null;
            }

            return result.Select(GetFileSource);
        }
        finally {
            OnFileDialogClosed();
        }
    }

    private IStorageProvider GetStorageProvider() {
        return EditorApp.TopLevel.StorageProvider;
    }

    public async Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null) {
        if (_isDialogOpened)
            return null;

        try {
            OnFileDialogOpened();


            var options = new FilePickerSaveOptions();
            options.FileTypeChoices = new[]
            {
                new FilePickerFileType("Pix2d supported images") { Patterns = fileTypeFilter.Select(x=>"*" + x).ToArray() }
            };

            options.DefaultExtension = fileTypeFilter.FirstOrDefault().Trim('.');
            options.SuggestedFileName = defaultFileName;

            var sp = GetStorageProvider();

            var result = await sp.SaveFilePickerAsync(options);
            if (result == null) {
                return null;
            }
            return GetFileSource(result);
        }
        finally {
            OnFileDialogClosed();
        }
    }

    protected virtual IFileContentSource? GetFileSource(IStorageFile? file) {
        if (file == null)
            return null;

        var path = file.Path.LocalPath;
        return new NetFileSource(path);
    }

    public async Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null) {
        if (_isDialogOpened)
            return null;
        try {
            OnFileDialogOpened();

            var dlg = new OpenFolderDialog();

            dlg.Title = "Select folder...";

            var result = await dlg.ShowAsync(DialogService.Window);
            return new NetFolder(result);
        }
        finally {
            _isDialogOpened = false;
            OnFileDialogClosed();
        }
    }

    public async Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false) {
        var path = Path.Combine(Pix2DApp.AppFolder, name);
        if (deleteIfExist && Directory.Exists(path))
            Directory.Delete(path, true);
        return new NetFolder(path);
    }

    public async Task<IFileContentSource> GetFileContentSourceAsync(string fileName) {
        return new NetFileSource(fileName);
    }

    public void AddToMru(IFileContentSource fileSource) {
        try {
            var mruList = SettingsService.Get<HashSet<MruRecord>>("mru")?.ToHashSet() ?? new HashSet<MruRecord>();
            mruList.Add(new MruRecord(fileSource));
            SettingsService.Set("mru", mruList);

            Messenger.Send(new MruChangedMessage());
        }
        catch (Exception ex) {
            Logger.LogException(ex);
        }
    }

    public async Task<List<IFileContentSource>> GetMruFilesAsync() {
        try {
            var mruList = SettingsService.Get<HashSet<MruRecord>>("mru")?.ToHashSet();

            return mruList?
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new NetFileSource(x.Path) {
                    Title = x.Name
                } as IFileContentSource).ToList() ?? new List<IFileContentSource>();
        }
        catch (Exception ex) {
            Logger.LogException(ex);
        }
        return new List<IFileContentSource>();
    }

    public Task<bool> IsFileExistsAsync(IFileContentSource fileSource) {
        return Task.FromResult(fileSource != null && File.Exists(fileSource.Path));
    }

    public async Task<IWriteDestinationFolder> GetFolder(string path) {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var fdi = new DirectoryInfo(path);

        return new NetFolder(fdi.FullName);
    }

    protected virtual void OnFileDialogOpened() {
        _isDialogOpened = true;
    }

    protected virtual void OnFileDialogClosed() {
        _isDialogOpened = false;
    }
}
