using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Platform.Storage.FileIO;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Common.FileSystem;
using Pix2d.Primitives;
using SkiaNodes.Serialization;

namespace Pix2d.Services;

public class AvaloiaNetFileService : IFileService
{
    public event EventHandler FileDialogOpened;
    public event EventHandler FileDialogClosed;
    public event EventHandler MruChanged;

    private Dictionary<string, string> _contextPaths;
    public Dictionary<string, string> ContextPaths => GetContextPaths();
    private Dictionary<string, string> GetContextPaths()
    {
        if (_contextPaths != null) return _contextPaths;

        var cts = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>()
            .Get<Dictionary<string, string>>(SettingsConstants.FileServiceContexts);

        _contextPaths = cts ?? new Dictionary<string, string>();

        return _contextPaths;
    }

    public IFileContentSource FileOpenOnStartup { get; set; }
    public AvaloniaDialogService DialogService { get; }

    private bool _isDialogOpened;
    private readonly Dictionary<string, IDataStorage> _storages = new Dictionary<string, IDataStorage>();

    public AvaloiaNetFileService(IDialogService dialogService)
    {
        DialogService = dialogService as AvaloniaDialogService;
    }

    public async Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false,
        string contextKey = null)
    {
        // проверяем и предотвращаем повтороное открытие диалога
        if (_isDialogOpened)
            return null;

        try
        {
            OnFileDialogOpened();

            var sp = GetStorageProvider();

            var options = new FilePickerOpenOptions();

            if (!string.IsNullOrWhiteSpace(contextKey) && ContextPaths.TryGetValue(contextKey, out var contextPath))
            {
                if (Directory.Exists(contextPath))
                    options.SuggestedStartLocation = new BclStorageFolder(contextPath);
            }


            var result = await sp.OpenFilePickerAsync(options);
            if (result == null || !result.Any())
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(contextKey) && result.First().TryGetUri(out var path))
            {
                ContextPaths[contextKey] = System.IO.Path.GetDirectoryName(path.AbsolutePath);
                DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>().Set(SettingsConstants.FileServiceContexts, ContextPaths);
            }

            return result.Select(x =>
            {
                result.First().TryGetUri(out var p);
                return new NetFileSource(p.AbsolutePath);
            });
        }
        finally
        {
            OnFileDialogClosed();
        }
    }

    private IStorageProvider GetStorageProvider()
    {
        return EditorApp.TopLevel.StorageProvider;
    }

    public async Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null)
    {
        if (_isDialogOpened)
            return null;

        try
        {
            OnFileDialogOpened();

            var sfd = new SaveFileDialog();
            //sfd.Filter = string.Join("|", fileTypeFilter.Select(x => x + "|*" + x));
            //sfd.DefaultExt = fileTypeFilter[0];

            //sfd.FileName = defaultFileName;

            if (!string.IsNullOrWhiteSpace(contextKey))
            {
                if (ContextPaths.TryGetValue(contextKey, out var contextPath))
                {
                    //sfd.InitialDirectory = contextPath;
                }
            }

            var result = await sfd.ShowAsync(DialogService.Window);

            if (!string.IsNullOrWhiteSpace(result))
            {
                if (!string.IsNullOrWhiteSpace(contextKey))
                {
                    ContextPaths[contextKey] = System.IO.Path.GetDirectoryName(result);
                    DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>().Set(SettingsConstants.FileServiceContexts, _contextPaths);
                }

                return new NetFileSource(result);
            }
        }
        finally
        {
            OnFileDialogClosed();
        }

        return null;
    }

    public async Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null)
    {
        if (_isDialogOpened)
            return null;
        try
        {
            OnFileDialogOpened();

            var dlg = new OpenFolderDialog();

            dlg.Title = "Select folder...";
            // dlg.IsFolderPicker = true;
            //dlg.InitialDirectory = currentDirectory;

            // dlg.AddToMostRecentlyUsedList = false;
            // dlg.AllowNonFileSystemItems = false;
            //dlg.DefaultDirectory = currentDirectory;
            // dlg.EnsureFileExists = true;
            // dlg.EnsurePathExists = true;
            // dlg.EnsureReadOnly = false;
            // dlg.EnsureValidNames = true;
            // dlg.Multiselect = false;
            // dlg.ShowPlacesList = true;


            if (!string.IsNullOrWhiteSpace(contextKey))
            {
                if (ContextPaths.TryGetValue(contextKey, out var contextPath))
                {
                    //dlg.InitialDirectory = contextPath;
                }
            }

            var result = await dlg.ShowAsync(DialogService.Window);
            if (string.IsNullOrWhiteSpace(result))
            {
                if (!string.IsNullOrWhiteSpace(contextKey))
                {
                    ContextPaths[contextKey] = result;
                    DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>().Set(SettingsConstants.FileServiceContexts, _contextPaths);
                }

                var folder = result;
                return new NetFolder(folder);
            }


        }
        finally
        {
            _isDialogOpened = false;
            OnFileDialogClosed();
        }
        return null;
    }

    public async Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false)
    {
        var path = System.IO.Path.Combine(Pix2DApp.AppFolder, name);
        if (deleteIfExist && Directory.Exists(path))
            Directory.Delete(path, true);
        return new NetFolder(path);
    }

    public async Task<IFileContentSource> GetFileContentSourceAsync(string fileName)
    {
        return new NetFileSource(fileName);
    }

    public void AddToMru(IFileContentSource fileSource)
    {
        try
        {
            var settingService = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>();
            var mruList = settingService.Get<HashSet<MruRecord>>("mru")?.ToHashSet() ?? new HashSet<MruRecord>();
            mruList.Add(new MruRecord(fileSource));
            settingService.Set("mru", mruList);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    public async Task<List<IFileContentSource>> GetMruFilesAsync()
    {
        try
        {
            var settingService = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>();
            var mruList = settingService.Get<HashSet<MruRecord>>("mru")?.ToHashSet();

            return mruList?
                .Where(x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new NetFileSource(x.Path)
                {
                    Title = x.Name
                } as IFileContentSource).ToList() ?? new List<IFileContentSource>();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        return new List<IFileContentSource>();
    }

    public Task<bool> IsFileExistsAsync(IFileContentSource fileSource)
    {
        return Task.FromResult(fileSource != null && File.Exists(fileSource.Path));
    }

    public async Task<IWriteDestinationFolder> GetFolder(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var fdi = new DirectoryInfo(path);

        return new NetFolder(fdi.FullName);
    }

    public IDataStorage GetDataStorage(string key)
    {
        return _storages[key];
    }

    public void SetDataStorage(string key, IDataStorage dataStorage)
    {
        _storages[key] = dataStorage;
    }

    protected virtual void OnFileDialogOpened()
    {
        _isDialogOpened = true;
        FileDialogOpened?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnFileDialogClosed()
    {
        _isDialogOpened = false;
        FileDialogClosed?.Invoke(this, EventArgs.Empty);
    }
}
