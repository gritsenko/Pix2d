using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Common;
using Pix2d.Common.FileSystem;
using Pix2d.Primitives;
using SkiaNodes.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Pix2d.Services
{
    public class SharedFileService : IFileService
    {
        public event EventHandler FileDialogOpened;
        public event EventHandler FileDialogClosed;
        public event EventHandler MruChanged;

        Dictionary<string, string> _contextPaths = new Dictionary<string, string>();

        public IFileContentSource FileOpenOnStartup { get; set; }

        private bool _isDialogOpened;
        private readonly Dictionary<string, IDataStorage> _storages = new Dictionary<string, IDataStorage>();

        public SharedFileService()
        {
            var cts = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>().Get<Dictionary<string, string>>(SettingsConstants.FileServiceContexts);
            if (cts != null)
            {
                _contextPaths = cts;
            }
        }

        public virtual async Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false,
            string contextKey = null)
        {
            // проверяем и предотвращаем повтороное открытие диалога
            if (_isDialogOpened)
                return null;

            var dlgSrv = CommonServiceLocator.ServiceLocator.Current.GetInstance<IDialogService>();
            //var dlg = new FilePickerDialog();
            //dlg.Files = GetFilesFromAppFolder("*" + fileTypeFilter.FirstOrDefault());
            //var result = await dlgSrv.ShowDialogAsync(dlg);

            //if (result.Value == true)
            //{

            //    try
            //    {
            //        OnFileDialogOpened();

            //        return dlg.SelectedFile.Yield();
            //    }
            //    finally
            //    {
            //        OnFileDialogClosed();
            //    }

            //}

            return null;
        }

        public IEnumerable<IFileContentSource> GetFilesFromAppFolder(string filter)
        {
            var dir = Pix2DApp.AppFolder;

            var dirFiles = string.IsNullOrWhiteSpace(filter)
                ? Directory.GetFiles(dir)
                : Directory.GetFiles(dir, filter);

            foreach (var file in dirFiles)
            {
                var path = Path.Combine(dir, file);
                yield return new NetFileSource(path);
            }
        }

        public virtual async Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null)
        {
            if (_isDialogOpened)
                return null;

            try
            {
                OnFileDialogOpened();
                //Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                //var dir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                //                var dir = Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments);

                var dlgSrv = CommonServiceLocator.ServiceLocator.Current.GetInstance<IDialogService>();

                var file = await dlgSrv.ShowInputDialogAsync("Enter file name", "SaveAsync as", defaultFileName);

                if (file == null)
                {
                    return null;
                }

                if (!fileTypeFilter.Any(x => file.EndsWith(x)))
                {
                    file += "." + fileTypeFilter.First().TrimStart('.');
                }

                var dir = Pix2DApp.AppFolder;
                var path = Path.Combine(dir, file);

                var sf = new NetFileSource(path);
                return sf;

            }
            finally
            {
                OnFileDialogClosed();
            }
        }

        public async Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null)
        {
            if (_isDialogOpened)
                return null;
            try
            {
                OnFileDialogOpened();

                //var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                //folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                //folderPicker.FileTypeFilter.Add("*");

                if (!string.IsNullOrWhiteSpace(contextKey))
                {
                    if (_contextPaths.TryGetValue(contextKey, out var contextPath))
                    {
                        //todo: добавить начальную директорию
                        //folderPicker..InitialDirectory = contextPath;
                    }
                }


                //                var folder = await folderPicker.PickSingleFolderAsync();
                //               if (folder != null)
                {
                    // Application now has read/write access to all contents in the picked folder
                    // (including other sub-folder contents)
                    //Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);

                    //todo: сохранить начальную директорию
                    //if (!string.IsNullOrWhiteSpace(contextKey))
                    //{
                    //    _contextPaths[contextKey] = dlg.FileName;
                    //    ServiceLocator.Current.GetInstance<ISettingsService>().Set("fileServiceContexts", _contextPaths);
                    //}

                    //return new UwpFolder(folder);
                }
            }
            finally
            {
                _isDialogOpened = false;
                OnFileDialogClosed();
            }
            return null;
        }

        public async Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExists = false)
        {
            var path = System.IO.Path.Combine(Pix2DApp.AppFolder, name);
            if (deleteIfExists && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            return new NetFolder(path);
        }

        public async Task<IFileContentSource> GetFileContentSourceAsync(string fileName)
        {
            return default;
            //return new UwpFileSource(await StorageFile.GetFileFromPathAsync(fileName));
        }

        public virtual void AddToMru(IFileContentSource fileSource)
        {
            if (fileSource == null)
                throw new ArgumentNullException("FileSource can't be null!");

            var settingService = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>();
            var mruList = settingService.Get<MruRecord[]>("mru") ?? new MruRecord[0];

            var hs = new HashSet<MruRecord>(mruList);

            hs.Add(new MruRecord(fileSource));

            settingService.Set("mru", hs);
            //JumpList.AddToRecentCategory(fileSource.Path);

            OnMruChanged();
        }

        public virtual async Task<List<IFileContentSource>> GetMruFilesAsync()
        {
            var settingService = DefaultServiceLocator.ServiceLocatorProvider().GetInstance<ISettingsService>();
            var mruList = settingService.Get<HashSet<MruRecord>>("mru")?.ToList();
            return mruList?.Select(x => new NetFileSource(x.Path) as IFileContentSource).ToList() ?? new List<IFileContentSource>();
        }

        public Task<bool> IsFileExistsAsync(IFileContentSource fileSource)
        {
            return Task.FromResult(fileSource != null && System.IO.File.Exists(fileSource.Path));
        }

        //public async Task<IWriteDestinationFolder> GetFolder(string path)
        //{
        //    if (!Directory.Exists(path))
        //        Directory.CreateDirectory(path);

        //    var fdi = new DirectoryInfo(path);

        //    return new UwpFolder(fdi.FullName);
        //}

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

        protected virtual void OnMruChanged()
        {
            MruChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}


