using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.Abstract.Platform
{
    public interface IFileService
    {
        Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false, string contextKey = null);

        Task<IFileContentSource?> GetFileToSaveWithDialogAsync(string[] fileTypeFilter, string contextKey = null, string defaultFileName = null);
        Task<IWriteDestinationFolder> GetFolderToExportWithDialogAsync(string contextKey = null);

        Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false);

        Task<IFileContentSource> GetFileContentSourceAsync(string fileName);

        void AddToMru(IFileContentSource fileSource);
        Task<List<IFileContentSource>> GetMruFilesAsync();
    }
}
