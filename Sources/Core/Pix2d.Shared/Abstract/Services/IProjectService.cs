using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Project;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

public interface IProjectService
{
    /// <summary>
    /// Save current changes
    /// </summary>
    Task SaveCurrentProjectAsync();

    /// <summary>
    /// Save current project as using file dialog
    /// </summary>
    /// <param name="saveAsType"></param>
    /// <returns></returns>
    Task SaveCurrentProjectAsAsync(ExportImportProjectType saveAsType);
    
    /// <summary>
    /// Try to open supported file via open file dialog
    /// </summary>
    Task OpenFilesAsync();

    /// <summary>
    /// Try to open file directly from files
    /// </summary>
    /// <param name="file">Collection of files to open</param>
    Task OpenFilesAsync(IEnumerable<IFileContentSource> file);

    /// <summary>
    /// Create new project with specified sprite size
    /// </summary>
    /// <param name="newProjectSize">Size of new canvas</param>
    Task CreateNewProjectAsync(SKSize newProjectSize);

    /// <summary>
    /// Retrieve recent opened projects collection
    /// </summary>
    /// <returns>Collection of recent projects</returns>
    Task<ProjectsCollection> GetRecentProjectsListAsync();


    /// <summary>
    /// Rename current open project (useful when you don't have direct access to device's filesystem
    /// </summary>
    Task RenameCurrentProjectAsync();
}