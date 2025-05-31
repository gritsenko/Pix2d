#nullable enable
using System.Runtime.CompilerServices;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Infrastructure;
using Pix2d.Infrastructure.Tasks;
using Pix2d.Messages;
using Pix2d.Project;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Services.Project;

public class ProjectService : IProjectService, ISessionProjectLoader
{
    private const string ProjectsFolder = "projects";
    private AppState AppState { get; }
    private IImportService ImportService { get; }

    private ProjectState ProjectState => AppState.CurrentProject;

    private IMessenger Messenger { get; }
    public IFileService FileService { get; }
    private IDialogService DialogService { get; }

    public ProjectService(AppState appState, 
        IImportService importService,
        IMessenger messenger,
        IFileService fileService,
        IDialogService dialogService)
    {
        Messenger = messenger;
        FileService = fileService;
        DialogService = dialogService;
        AppState = appState;
        ImportService = importService;

        Messenger.Register<OperationInvokedMessage>(this, _ =>
        {
            if (HasUnsavedChanges) return;
            HasUnsavedChanges = true;
        });
    }

    public async Task<ProjectsCollection> GetRecentProjectsListAsync()
    {
        var mrus = await GetRecentProjectsAsync();
        return new ProjectsCollection(mrus);
    }

    public async Task RenameCurrentProjectAsync()
    {
        var currentName = GetDefaultFileName();
        var result = await DialogService.ShowInputDialogAsync("Rename current project", "Rename project", currentName);

        if (string.IsNullOrWhiteSpace(result))
            return;

        var currentFile = ProjectState.File;
        if (currentFile == null)
        {
            if (AppState.Settings.UseInternalFolder)
            {
                var folder = await FileService.GetLocalFolderAsync(ProjectsFolder);
                var file = GetUniqueProjectFile(folder, result);
                await SaveCurrentProjectToFileAsync(file);
            }
            else
            {
                var filePickerResult = await GetFileToExport(".pix2d", result);
                await filePickerResult.MatchAsync(async file => await SaveCurrentProjectToFileAsync(file));
            }
        }
        else
        {
            var sourcePath = currentFile.Path;
            var targetPath = Path.Join(Path.GetDirectoryName(sourcePath), result + Path.GetExtension(sourcePath));
            try
            {
                File.Move(sourcePath, targetPath);

                FileService.RemoveFromMru(sourcePath);
                var newFile = await FileService.GetFileContentSourceAsync(targetPath);
                FileService.AddToMru(newFile);

                ProjectState.File = newFile;

                Messenger.Send(new ProjectSavedMessage());
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }
    }

    private bool HasUnsavedChanges
    {
        get => ProjectState.HasUnsavedChanges;
        set => ProjectState.HasUnsavedChanges = value;
    }

    private void UpdateProjectNameInWindowTitle()
    {
        AppState.WindowTitle = ProjectState.Title + (ProjectState.HasUnsavedChanges ? "*" : "");
    }

    public async Task SaveCurrentProjectAsync()
    {
        OpLog();
        //save into local app folder (android or web)
        if (AppState.Settings.UseInternalFolder)
        {
            var folder = await FileService.GetLocalFolderAsync(ProjectsFolder);
            var file = GetUniqueProjectFile(folder);
            await SaveCurrentProjectToFileAsync(file);
            return;
        }

        // we are editing existing pix2d project
        if (ProjectState.File is { Extension: ".pix2d" })
        {
            await SaveCurrentProjectToFileAsync(ProjectState.File);
            return;
        }

        //new project
        await SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);
    }

    private IFileContentSource GetUniqueProjectFile(IWriteDestinationFolder folder, string? defaultName = null)
    {
        if (string.IsNullOrWhiteSpace(defaultName))
            defaultName = GetDefaultFileName();

        var i = 0;
        var name = defaultName;
        while (folder.GetFileSource(name, ".pix2d").Exists)
        {
            name = $"{defaultName}({i})";
            i++;
        }

        return folder.GetFileSource(name, ".pix2d");
    }

    public async Task SaveCurrentProjectAsAsync(ExportImportProjectType saveAsType)
    {
        OpLog();
        var filePickerResult = await GetFileToExport(saveAsType.FileExtension);
        await filePickerResult.MatchAsync(SaveCurrentProjectToFileAsync);
    }

    private async Task<Result<IFileContentSource, FileDialogResultError>> GetFileToExport(string filetype,
        string? defaultName = null) =>
        await FileService.GetFileToSaveWithDialogAsync([filetype], "project", defaultName ?? GetDefaultFileName());

    private string GetDefaultFileName()
    {
        const string defaultName = "new_project";
        var projectName = string.IsNullOrWhiteSpace(ProjectState.FileName)
            ? defaultName
            : Path.GetFileNameWithoutExtension(ProjectState.FileName);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = defaultName;
        }

        return projectName;
    }

    private async Task SaveCurrentProjectToFileAsync(IFileContentSource targetFile)
    {
        using var uiBlocker = new UiBlocker("Saving project...");
        await ProjectPacker.WriteProjectAsync(targetFile, AppState.CurrentProject.SceneNode);

        ProjectState.File = targetFile;
        HasUnsavedChanges = false;
        FileService.AddToMru(targetFile);
        OnProjectSaved();
    }

    protected virtual void OnProjectSaved()
    {
        Messenger.Send(new ProjectSavedMessage());
        UpdateProjectNameInWindowTitle();
    }

    public async Task OpenFilesAsync()
    {
        var extensions = ExportImportProjectType.GetSupportedImportFileExtensions();
        var files = await FileService.OpenFileWithDialogAsync(extensions, true, "project");
        await OpenFilesAsync(files.ToArray());
    }

    public async Task OpenProjectFromSessionAsync(IFileContentSource sessionFile)
    {
        using var uiBlocker = new UiBlocker("Loading previous session...");
        var scene = await NewSceneFactory.GetNewSceneFromFiles([sessionFile], ImportService);
        HasUnsavedChanges = true;
        OnProjectLoaded(scene);
    }
    public async Task OpenFilesAsync(IEnumerable<IFileContentSource> files)
    {
        OpLog();

        var fileContentSources = files.ToArray();

        if (!fileContentSources.Any())
            return;

        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return;

        CloseCurrentProject();

        using var uiBlocker = new UiBlocker("Loading project...");
        var scene = await NewSceneFactory.GetNewSceneFromFiles(fileContentSources, ImportService);

        var file = fileContentSources.First();
        if (!OperatingSystem.IsBrowser())
        {
            var folder = await FileService.GetLocalFolderAsync(ProjectsFolder);
            if (AppState.Settings.UseInternalFolder && !file.Path.StartsWith(folder.Path))
            {
                var projectName = Path.GetFileNameWithoutExtension(file.Path);
                file = GetUniqueProjectFile(folder, projectName);
                HasUnsavedChanges = true;
            }
            else
            {
                HasUnsavedChanges = false;
            }

            FileService.AddToMru(file);
        }

        ProjectState.File = file;

        OnProjectLoaded(scene);
    }

    private void OnProjectLoaded(SKNode scene)
    {
        Logger.Trace("Project loaded");
        Messenger.Send(new ProjectLoadedMessage(scene));
        UpdateProjectNameInWindowTitle();
    }

    private void CloseCurrentProject()
    {
        ProjectState.File = null;
        HasUnsavedChanges = false;
        Messenger.Send(new ProjectCloseMessage());
    }

    private async Task<bool> AskSaveCurrentProject()
    {
        var result = await DialogService.ShowUnsavedChangesInProjectDialog();

        if (result == UnsavedChangesDialogResult.Cancel)
            return false;

        if (result == UnsavedChangesDialogResult.Yes)
            await SaveCurrentProjectAsync();

        return true;
    }

    public async Task CreateNewProjectAsync(SKSize newProjectSize)
    {
        OpLog(newProjectSize.Width + "x" + newProjectSize.Height);
        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return;

        CloseCurrentProject();

        var scene = NewSceneFactory.GetNewScene(newProjectSize);
        OnProjectLoaded(scene);
    }

    public async Task<IFileContentSource[]> GetRecentProjectsAsync()
    {
        var result = await FileService.GetMruFilesAsync();
        result.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));
        return result.ToArray();
    }

    public static void OpLog(string? info = null, [CallerMemberName] string? caller = null)
    {
        SessionLogger.OpLog(info, caller);
    }
}