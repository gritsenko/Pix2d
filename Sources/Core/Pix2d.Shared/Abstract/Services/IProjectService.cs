using System.Threading.Tasks;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Project;
using SkiaSharp;

namespace Pix2d.Abstract.Services
{
    public interface IProjectService
    {
        /// <summary>
        /// Save current changes
        /// </summary>
        /// <returns>true if operation was complete successful</returns>
        Task<bool> SaveCurrentProjectAsync();

        /// <summary>
        /// Open file picker and save project to chosen file
        /// </summary>
        /// <param name="saveAsType"></param>
        /// <returns>true if operation was complete successful</returns>
        Task<bool> SaveCurrentProjectAsAsync(ExportImportProjectType saveAsType);
        
        /// <summary>
        /// Saves current pix2d project to file
        /// </summary>
        /// <param name="targetFile">destination project file</param>
        /// <param name="isSessionMode">if true, notifies other modules that action called by user from menu or hot-key, so saved project will be added to recent files list and HasUnsavedChanges flag will be reset. [false] value used by session backup process</param>
        /// <returns>true if operation was complete successful</returns>
        Task<bool> SaveCurrentProjectToFileAsync(IFileContentSource targetFile, bool isSessionMode = false);

        Task<bool> OpenFilesAsync();
        Task<bool> OpenFilesAsync(IFileContentSource[] file, bool isLoadingFromLocalSession = false);

        Task CreateNewProjectAsync(SKSize newProjectSize);
        Task<IFileContentSource[]> GetRecentProjectsAsync();
        string GetDefaultFileName();
        string CurrentProjectName { get; }

        Task<ProjectsCollection> GetProjectsListAsync();
        Task RenameCurrentProjectAsync();
    }
}