#nullable enable
using System.Runtime.CompilerServices;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.CommonNodes;
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
    private IProjectActivationService ProjectActivationService { get; }
    private IOperationService OperationService { get; }
    private IPlatformStuffService PlatformStuffService { get; }
    private IAutoSaveService AutoSaveService { get; }
    private IExportService ExportService { get; }

    private bool SupportsMultipleProjects => PlatformStuffService.SupportsMultipleProjects;

    public ProjectService(AppState appState,
        IImportService importService,
        IMessenger messenger,
        IFileService fileService,
        IDialogService dialogService,
        IProjectActivationService projectActivationService,
        IOperationService operationService,
        IPlatformStuffService platformStuffService,
        IAutoSaveService autoSaveService,
        IExportService exportService)
    {
        Messenger = messenger;
        FileService = fileService;
        DialogService = dialogService;
        AppState = appState;
        ImportService = importService;
        ProjectActivationService = projectActivationService;
        OperationService = operationService;
        PlatformStuffService = platformStuffService;
        AutoSaveService = autoSaveService;
        ExportService = exportService;

        Messenger.Register<OperationInvokedMessage>(this, msg =>
        {
            if (HasUnsavedChanges) return;
            // Marquee creation and in-flight transform live entirely in transient UI state; only the
            // commit step (ApplyTransformOperation, also ISpriteEditorOperation) writes to a layer. Without
            // this filter, drawing a selection would flip the "unsaved" star on the title bar even though
            // nothing the .pix2d file cares about has changed.
            if (msg.Operation is ISelectionFlowOperation && msg.Operation is not ISpriteEditorOperation)
                return;
            HasUnsavedChanges = true;
        });

        // Some fresh-load paths bypass this service entirely (autosave crash recovery sends
        // ProjectLoadedMessage directly), so the tab list is reconciled from the message: the
        // current project must always be a LoadedProjects entry for the tab bar to show it.
        Messenger.Register<ProjectLoadedMessage>(this, _ => EnsureCurrentProjectIsListed());

        // A tab switch changes which project the window title describes.
        Messenger.Register<ProjectActivatedMessage>(this, _ => UpdateProjectNameInWindowTitle());
    }

    private void EnsureCurrentProjectIsListed()
    {
        var current = AppState.CurrentProject;
        var index = AppState.LoadedProjects.IndexOf(current);
        if (index >= 0)
        {
            AppState.ActiveProjectIndex = index;
            return;
        }

        AppState.LoadedProjects.Add(current);
        AppState.ActiveProjectIndex = AppState.LoadedProjects.Count - 1;
        Messenger.Send(ProjectsListChangedMessage.Default);
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

        // we are editing existing pix2d project (case-insensitive extension, matching the PNG path below)
        if (ProjectState.File is { } projectFile
            && string.Equals(projectFile.Extension, ".pix2d", StringComparison.OrdinalIgnoreCase))
        {
            await SaveCurrentProjectToFileAsync(projectFile);
            return;
        }

        // Project opened from a flat PNG that is still a single sprite / single layer / single frame:
        // overwrite the original image in place instead of prompting a ".pix2d" Save As (#200). Once the
        // user adds a layer or a frame (or another artboard) the shape no longer maps to a flat PNG, so we
        // fall through to the project save below.
        if (ProjectState.File is { } pngFile
            && string.Equals(pngFile.Extension, ".png", StringComparison.OrdinalIgnoreCase)
            && ProjectState.SceneNode?.Nodes.OfType<Pix2dSprite>().ToArray() is { Length: 1 } sprites
            && sprites[0].Layers.Count() == 1
            && sprites[0].GetFramesCount() == 1)
        {
            using (new UiBlocker("Saving image..."))
                await ExportService.ExportNodesToFileAsync(pngFile, sprites, 1);

            HasUnsavedChanges = false;
            FileService.AddToMru(pngFile);
            OnProjectSaved();
            return;
        }

        //new project
        await SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);
    }

    private IFileContentSource GetUniqueProjectFile(IWriteDestinationFolder folder, string? defaultName = null)
    {
        if (string.IsNullOrWhiteSpace(defaultName))
            defaultName = GetDefaultFileName();

        defaultName = SanitizeProjectFileName(defaultName);

        var i = 0;
        var name = defaultName;
        while (folder.GetFileSource(name, ".pix2d").Exists)
        {
            name = $"{defaultName}({i})";
            i++;
        }

        return folder.GetFileSource(name, ".pix2d");
    }

    // Guards against names sourced from external file pickers / content providers, which can hand
    // back invalid-for-filesystem characters or (rarely) a display name hundreds of chars long -
    // either of which would otherwise blow the OS path-length limit deep inside File.OpenWrite.
    private const int MaxProjectFileNameLength = 100;

    private static string SanitizeProjectFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalidChars) >= 0)
            name = string.Concat(name.Select(c => invalidChars.Contains(c) ? '_' : c));

        return name.Length > MaxProjectFileNameLength
            ? name[..MaxProjectFileNameLength]
            : name;
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
        if (AppState.CurrentProject.SceneNode != null)
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

        // Desktop: open into a NEW tab, keeping the current project. The startup placeholder
        // (no scene yet) still goes through the regular replace path below so the first real
        // project doesn't leave an empty phantom tab behind.
        if (SupportsMultipleProjects && AppState.CurrentProject.SceneNode != null)
        {
            await OpenFilesInNewTabAsync(fileContentSources);
            return;
        }

        if (HasUnsavedChanges && !await AskSaveCurrentProject())
            return;

        // Build the new scene BEFORE closing the current project: an open that fails (a file Pix2d
        // can't decode, a corrupt project) used to leave the editor half-closed — ProjectState.File
        // cleared and ProjectCloseMessage already sent — so the user lost the link to their own file
        // and got nothing in return. Now a failed open changes nothing.
        using var uiBlocker = new UiBlocker("Loading project...");
        SKNode scene;
        try
        {
            scene = await NewSceneFactory.GetNewSceneFromFiles(fileContentSources, ImportService);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            // show error to user and abort loading
            try { DialogService.Alert($"Failed to load project: {ex.Message}", "Load project error"); } catch { }
            return;
        }

        CloseCurrentProject();

        var file = fileContentSources.First();
        if (!OperatingSystem.IsBrowser())
        {
            var folder = await FileService.GetLocalFolderAsync(ProjectsFolder);
            if (AppState.Settings.UseInternalFolder && !file.Path.StartsWith(folder.Path))
            {
                // file.Title is the display name (e.g. from SAF's DISPLAY_NAME column); file.Path
                // for a content:// source is the raw URI, whose document-id segment can itself
                // encode the source folder path (.../document/primary%3ADownload%2F...) with no
                // literal '/', so GetFileNameWithoutExtension(file.Path) would treat that whole
                // blob as the file name and blow past the OS path-length limit (#crash).
                var projectName = Path.GetFileNameWithoutExtension(file.Title);
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

    private async Task OpenFilesInNewTabAsync(IFileContentSource[] fileContentSources)
    {
        // Already open in another tab — just switch to it instead of loading a copy.
        var requestedPath = fileContentSources[0].Path;
        var existing = AppState.LoadedProjects.FirstOrDefault(p =>
            !string.IsNullOrEmpty(p.File?.Path) &&
            string.Equals(p.File!.Path, requestedPath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ProjectActivationService.ActivateProject(existing);
            return;
        }

        using var uiBlocker = new UiBlocker("Loading project...");
        SKNode scene;
        try
        {
            scene = await NewSceneFactory.GetNewSceneFromFiles(fileContentSources, ImportService);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            try { DialogService.Alert($"Failed to load project: {ex.Message}", "Load project error"); } catch { }
            return;
        }

        var file = fileContentSources.First();
        var project = new ProjectState();
        var hasUnsavedChanges = false;

        if (!OperatingSystem.IsBrowser())
        {
            var folder = await FileService.GetLocalFolderAsync(ProjectsFolder);
            if (AppState.Settings.UseInternalFolder && !file.Path.StartsWith(folder.Path))
            {
                var projectName = Path.GetFileNameWithoutExtension(file.Title);
                file = GetUniqueProjectFile(folder, projectName);
                hasUnsavedChanges = true;
            }

            FileService.AddToMru(file);
        }

        project.File = file;
        project.HasUnsavedChanges = hasUnsavedChanges;

        AddProjectTabAndLoadScene(project, scene);
    }

    /// <summary>
    /// Registers <paramref name="project"/> as a new tab, makes it current and runs the regular
    /// fresh-load pipeline for its scene (history clear, sprite activation, ShowAll, grid reset).
    /// </summary>
    private void AddProjectTabAndLoadScene(ProjectState project, SKNode scene)
    {
        AppState.LoadedProjects.Add(project);
        ProjectActivationService.BeginNewProjectActivation(project);

        OnProjectLoaded(scene);

        Messenger.Send(ProjectsListChangedMessage.Default);
        Messenger.Send(new ProjectActivatedMessage(project));
    }

    public async Task CloseProjectAsync(ProjectState project)
    {
        OpLog();

        if (project == null || !AppState.LoadedProjects.Contains(project))
            return;

        if (project.HasUnsavedChanges)
        {
            // Bring the project on screen so the user sees what the save prompt is about.
            ProjectActivationService.ActivateProject(project);
            if (!await AskSaveCurrentProject())
                return;
        }

        var index = AppState.LoadedProjects.IndexOf(project);
        if (index < 0)
            return;

        AppState.LoadedProjects.RemoveAt(index);

        if (AppState.LoadedProjects.Count == 0)
        {
            // Last tab closed: always keep an editable scene (mirrors the startup fallback).
            AddProjectTabAndLoadScene(new ProjectState(), NewSceneFactory.GetNewScene(new SKSize(64, 64)));
        }
        else
        {
            if (ReferenceEquals(AppState.CurrentProject, project))
            {
                var neighbor = AppState.LoadedProjects[Math.Min(index, AppState.LoadedProjects.Count - 1)];
                ProjectActivationService.ActivateProject(neighbor);
            }
            else
            {
                // Removing a tab to the left of the active one shifts indices.
                AppState.ActiveProjectIndex = AppState.LoadedProjects.IndexOf(AppState.CurrentProject);
            }

            Messenger.Send(ProjectsListChangedMessage.Default);
        }

        // Free the closed project only after the replacement scene is current: SetScene never
        // disposes the outgoing scene, so a tab close is the one place that unloads it.
        OperationService.RemoveHistory(project.Id);
        project.SceneNode?.Unload();
        project.SceneNode = null;
        project.FrameEditorNode = null;

        // Drop the tab's autosave session folder so a deliberately closed tab is not
        // resurrected on the next launch. Fire-and-forget: file I/O must not block the UI.
        _ = AutoSaveService.DiscardProjectSessionAsync(project.Id);
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

        // Analytics: every new-project entry point (File → New, Ctrl+T new tab, custom-size dialog)
        // funnels through here, mirroring how export tracking lives in the single ExportView.Export().
        Logger.LogEventWithParams("Project created", new Dictionary<string, string?>
        {
            { "Size", $"{newProjectSize.Width:0}x{newProjectSize.Height:0}" }
        });

        // Desktop: a new project opens in its own tab; the current one stays loaded, so no
        // save prompt is needed. Startup placeholder (no scene) still uses the replace path.
        if (SupportsMultipleProjects && AppState.CurrentProject.SceneNode != null)
        {
            AddProjectTabAndLoadScene(new ProjectState(), NewSceneFactory.GetNewScene(newProjectSize));
            return;
        }

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