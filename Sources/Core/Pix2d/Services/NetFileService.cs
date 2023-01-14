using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Common.FileSystem;
using SkiaNodes.Serialization;

namespace Pix2d.Services
{
    public class NetFileService : IFileService
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

        private bool _isDialogOpened;
        private readonly Dictionary<string, IDataStorage> _storages = new Dictionary<string, IDataStorage>();

        public Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false,
            string contextKey = null)
        {
            throw new NotImplementedException();
        }

        public Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null)
        {
            throw new NotImplementedException();
        }

        public Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null)
        {
            throw new NotImplementedException();
        }

        public Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false)
        {
            var path = System.IO.Path.Combine(Pix2DApp.AppFolder, name);
            if(deleteIfExist && Directory.Exists(path))
                Directory.Delete(path, true);
            return Task.FromResult<IWriteDestinationFolder>(new NetFolder(path));
        }

        public Task<IFileContentSource> GetFileContentSourceAsync(string fileName)
        {
            return Task.FromResult<IFileContentSource>(new NetFileSource(fileName));
        }

        public void AddToMru(IFileContentSource fileSource)
        {
            throw new NotImplementedException();
        }

        public Task<List<IFileContentSource>> GetMruFilesAsync()
        {
            return Task.FromResult(new List<IFileContentSource>());
        }

        public Task<bool> IsFileExistsAsync(IFileContentSource fileSource)
        {
            return Task.FromResult(fileSource != null && System.IO.File.Exists(fileSource.Path));
        }

        public Task<IWriteDestinationFolder> GetFolderAsync(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var fdi = new DirectoryInfo(path);

            return Task.FromResult<IWriteDestinationFolder>(new NetFolder(fdi.FullName));
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
}
