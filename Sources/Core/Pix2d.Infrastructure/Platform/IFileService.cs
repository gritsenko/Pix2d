#nullable enable
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Infrastructure;

namespace Pix2d.Abstract.Platform;

public interface IFileService
{
    /// <summary>
    /// Activates Open file dialog
    /// </summary>
    /// <param name="fileTypeFilter">file extensions array in format: .ext , e.g. [".png", ".jpg"] </param>
    /// <param name="allowMultiplyFiles">can user select several files</param>
    /// <param name="contextKey">define contextKey to separate saved folders for different contexts, for example: for project saving project files there can be one context and for Png exported files there will be another. So when you save project it will keep last directory. But whe you will export png It will open export folder by default</param>
    /// <returns>Collection of Files selected by user</returns>
    Task<IEnumerable<IFileContentSource>> OpenFileWithDialogAsync(string[] fileTypeFilter, bool allowMultiplyFiles = false, string? contextKey = null);

    Task<Result<IFileContentSource, FileDialogResultError>> GetFileToSaveWithDialogAsync(string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null);
    /// <summary>
    /// Shows file picker dialog and saves text content into selected file
    /// </summary>
    /// <param name="text">text to write</param>
    /// <param name="fileTypeFilter">file extensions array in format: .ext , e.g. [".png", ".jpg"] </param>
    /// <param name="contextKey">define contextKey to separate saved folders for different contexts, for example: for project saving project files there can be one context and for Png exported files there will be another. So when you save project it will keep last directory. But whe you will export png It will open export folder by default</param>
    /// <param name="defaultFileName">suggested file name that will be used by default on dialog opened</param>
    /// <returns></returns>
    Task<bool> SaveTextToFileWithDialogAsync(string text, string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null);

    /// <summary>
    /// Shows file picker dialog and saves stream content, when file selected
    /// </summary>
    /// <param name="streamProvider">Tasks that must return stream, called when file selected by user. Don't prepare data for saving until user selected </param>
    /// <param name="fileTypeFilter">file extensions array in format: .ext , e.g. [".png", ".jpg"] </param>
    /// <param name="contextKey">define contextKey to separate saved folders for different contexts, for example: for project saving project files there can be one context and for Png exported files there will be another. So when you save project it will keep last directory. But whe you will export png It will open export folder by default</param>
    /// <param name="defaultFileName">suggested file name that will be used by default on dialog opened</param>
    /// <returns></returns>
    Task<bool> SaveStreamToFileWithDialogAsync(Func<Task<Stream>> streamProvider, string[] fileTypeFilter, string? contextKey = null, string? defaultFileName = null);
    Task<IWriteDestinationFolder?> GetFolderToExportWithDialogAsync(string? contextKey = null);

    Task<IWriteDestinationFolder> GetLocalFolderAsync(string name, bool deleteIfExist = false);

    Task<IFileContentSource> GetFileContentSourceAsync(string fileName);

    void AddToMru(IFileContentSource fileSource);
    Task<List<IFileContentSource>> GetMruFilesAsync();
    void RemoveFromMru(string sourcePath);
}