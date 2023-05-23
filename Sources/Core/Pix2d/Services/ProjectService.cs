using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Abstract.UI;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Project;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Services;

public class ProjectService : IProjectService
{
    private AppState AppState { get; }

    private ProjectState ProjectState => AppState.CurrentProject as ProjectState;

    public IMessenger Messenger { get; }
    public IBusyController BusyController { get; }

    private IFileService FileService => ServiceLocator.Current.GetInstance<IFileService>();

    public string CurrentProjectName => ProjectState.FileName;

    public bool HasUnsavedChanges
    {
        get => ProjectState.HasUnsavedChanges;
        set => ProjectState.HasUnsavedChanges = value;
    }

    public ProjectService(AppState appState, IMessenger messenger, IBusyController busyController)
    {
        Messenger = messenger;
        BusyController = busyController;
        AppState = appState as AppState;

        Messenger.Register<OperationInvokedMessage>(this, e =>
        {
            if (!HasUnsavedChanges)
            {
                HasUnsavedChanges = true;
                UpdateProjectName();
            }
        });
    }

    private void UpdateProjectName(bool isSessionMode = false)
    {
        var name = "New Project";


        if (isSessionMode && ProjectState.LastSessionInfo.ProjectPath != null)
            name = ProjectState.LastSessionInfo.ProjectPath;
        else if (ProjectState.File != null)
            name = ProjectState.File.Path;

        if (HasUnsavedChanges) name += "*";

        AppState.WindowTitle = name;
        Messenger.Send(new ProjectNameChangedMessage(name));
    }

    public async Task<bool> SaveCurrentProjectAsync()
    {
        var file = ProjectState.File;
        if (file == null || !file.Exists || file.Extension != ".pix2d")
            return await SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);

        OpLog();
        Logger.Log("Saving project");
        if (AppState.IsBusy)
            throw new Exception("Trying to save project, while previous process not finished.");

        return await BusyController.RunLongTaskAsync(async () => await SaveCurrentProjectToFileAsync(file));
    }

    public async Task<bool> SaveCurrentProjectAsAsync(ExportImportProjectType saveAsType)
    {
        OpLog();
        Logger.Log("Saving project as");

        var file = await GetFileToExport(saveAsType.FileExtension, GetDefaultFileName());
        if (file == null)
            return false;

        if (saveAsType == ExportImportProjectType.Png)
            return await ExportToPng(file);

        return await BusyController.RunLongTaskAsync(async () => await SaveCurrentProjectToFileAsync(file));
    }

    private async Task<bool> ExportToPng(IFileContentSource file)
    {
        var sprite = AppState.CurrentProject.SceneNode.Nodes.OfType<DrawingContainerBaseNode>();
        var exportService = ServiceLocator.Current.GetInstance<IExportService>();
        await exportService.ExportNodesAsync(file, sprite, 1);
        FileService.AddToMru(file);
        return true;
    }

    private async Task<IFileContentSource> GetFileToExport(string filetype, string defaultName = null)
        => await FileService.GetFileToSaveWithDialogAsync(defaultName ?? CurrentProjectName, new[] { filetype }, "project");

    private string GetDefaultFileName()
        => !string.IsNullOrEmpty(CurrentProjectName) && !ProjectState.IsNewProject ? System.IO.Path.GetFileNameWithoutExtension(CurrentProjectName) : "new_project";

    public async Task<bool> SaveCurrentProjectToFileAsync(IFileContentSource targetFile, bool isSessionMode = false)
    {
        await ProjectPacker.WriteProjectAsync(targetFile, AppState.CurrentProject.SceneNode);

        if (isSessionMode) return true;

        ProjectState.File = targetFile;
        HasUnsavedChanges = false;
        FileService.AddToMru(targetFile);
        OnProjectSaved(isSessionMode);
        return true;
    }
    protected virtual void OnProjectSaved(bool isSessionMode)
    {
        Messenger.Send<ProjectSavedMessage>(default);
        UpdateProjectName(isSessionMode);
    }

    public async Task<bool> OpenFilesAsync()
    {
        var extensions = ExportImportProjectType.GetSupportedImportFileExtensions();
        var files = await FileService.OpenFileWithDialogAsync(extensions, true, "project");
        if (files == null)
            return false;

        return await OpenFilesAsync(files.ToArray());
    }

    public async Task<bool> OpenFilesAsync(IFileContentSource[] files, bool isSessionMode = false)
    {
        OpLog();

        if (files == null || !files.Any())
            return false;

        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return false;

        return await BusyController.RunLongTaskAsync(async () =>
            {
                var file = files.FirstOrDefault();
                CloseCurrentProject();
                var loader = GetLoaderFromExtension(file.Extension);
                var scene = await loader.Invoke(files);
                if (scene != default)
                {
                    if (isSessionMode)
                    {
                        HasUnsavedChanges = true;
                    }
                    else
                    {
                        FileService.AddToMru(file);
                        HasUnsavedChanges = false;
                        ProjectState.File = file;
                    }

                    OnProjectLoaded(scene, isSessionMode);
                }
            });
    }

    private Func<IEnumerable<IFileContentSource>, Task<SKNode>> GetLoaderFromExtension(string fileExtension)
    {
        var importService = ServiceLocator.Current.GetInstance<IImportService>();

        if (importService.CanImport(fileExtension))
            return TryToImport;

        return f => LoadProjectFiles(f.First());
    }

    private async Task<SKNode> TryToImport(IEnumerable<IFileContentSource> files)
    {
        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return null;

        var scene = GetNewScene(new SKSize(1, 1));

        await BusyController.RunLongTaskAsync(async () =>
        {
            CloseCurrentProject();
            if (scene.GetDescendants().OfType<Pix2dSprite>().FirstOrDefault() is Pix2dSprite targetSprite)
                await CoreServices.ImportService.TryImportToSprite(targetSprite, files.ToArray());
        });
        return scene;
    }

    private void OnProjectLoaded(SKNode scene, bool isSessionMode)
    {
        var bounds = scene.GetChildrenBounds();
        Logger.LogEventWithParams("Project loaded", new Dictionary<string, string>() { { "size", bounds.Size.ToString() } });
        OpLog(bounds.Size.Width + "x" + bounds.Size.Height);

        Messenger.Send(new ProjectLoadedMessage(scene, isSessionMode));
        UpdateProjectName(isSessionMode);
    }

    private async Task<SKNode> LoadProjectFiles(IFileContentSource file)
    {
        return await ProjectUnpacker.LoadProjectScene(file);
    }

    private void CloseCurrentProject()
    {
        ProjectState.File = null;
        HasUnsavedChanges = false;
        Messenger.Send(new ProjectCloseMessage());
    }

    private async Task<bool> AskSaveCurrentProject()
    {
        var dialogService = ServiceLocator.Current.GetInstance<IDialogService>();
        if (dialogService == null)
            return false;

        var result = await dialogService.ShowUnsavedChangesInProjectDialog();

        if (result == UnsavedChangesDialogResult.Cancel)
            return false;

        if (result == UnsavedChangesDialogResult.Yes)
            return await SaveCurrentProjectAsync();

        return true;
    }

    public async Task CreateNewProjectAsync(SKSize newProjectSize)
    {
        OpLog(newProjectSize.Width + "x" + newProjectSize.Height);
        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return;

        CloseCurrentProject();

        await BusyController.RunLongTaskAsync(async () =>
        {
            var scene = GetNewScene(newProjectSize);
            OnProjectLoaded(scene, false);
        });
    }

    private SKNode GetNewScene(SKSize newProjectSize)
    {
        var scene = new SKNode() { Name = "Scene" };
        var sprite = Pix2dSprite.CreateEmpty(newProjectSize);
        sprite.Name = "New Sprite";
        sprite.DesignerState.IsSelected = true;
        scene.Nodes.Add(sprite);
        return scene;
    }

    public async Task<IFileContentSource[]> GetRecentProjectsAsync()
    {
        var result = await FileService.GetMruFilesAsync();
        return result.ToArray();
    }

    public static void OpLog(string info = null, [CallerMemberName] string caller = null)
    {
        SessionLogger.OpLog(info, caller);
    }

}