#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pix2d.Abstract.Platform.FileSystem;

public interface IWriteDestinationFolder
{
    string Path { get; }

    IFileContentSource GetFileSource(string name, string extension = "png", bool overwrite = false);
    Task<IFileContentSource> GetFileSourceAsync(string name, string extension = "png", bool overwrite = false);
    Task<IFileContentSource> GetFileSourceToReadAsync(string name, string extension = "png");
    IWriteDestinationFolder GetSubfolder(string folderName);
    Task<IWriteDestinationFolder> GetSubfolderAsync(string folderName);
    void CopyTemplateFrom(string templatePath);
    Task ClearFolderAsync();
    Task<IEnumerable<IFileContentSource>> GetFilesAsync(string? subfolderPath = default);
}