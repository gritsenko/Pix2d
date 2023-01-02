using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Platform
{
    public interface IFileService
    {
        event EventHandler FileDialogOpened;
        event EventHandler FileDialogClosed;
        event EventHandler MruChanged;


        IFileContentSource FileOpenOnStartup { get; set; }

        Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false, string contextKey = null);

        Task<IFileContentSource> GetFileToSaveWithDialogAsync(string defaultFileName, string[] fileTypeFilter, string contextKey = null);
        Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null);

        Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false);

        Task<IFileContentSource> GetFileContentSourceAsync(string fileName);

        void AddToMru(IFileContentSource fileSource);
        Task<List<IFileContentSource>> GetMruFilesAsync();

        Task<bool> IsFileExistsAsync(IFileContentSource fileSource);

        //Task<IWriteDestinationFolder> GetFolder(string path);

        //IEnumerable<IFileContentSource> GetFilesFromAppFolder(string filter);

    }
}
