using System;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class FileCommands : CommandsListBase
{
    protected override string BaseName => "File";

    private void HideMainMenu()
    {
        var commandService = ServiceProvider.GetRequiredService<ICommandService>();
        commandService.GetCommandList<ViewCommands>()?.HideMainMenuCommand.Execute();
    }
    
    public Pix2dCommand New => GetCommand(async () =>
    {
        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64));
    }, "New", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand Open => GetCommand(async () =>
    {
        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().OpenFilesAsync();
    }, "Open...", new CommandShortcut(VirtualKeys.O, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand NewTab => GetCommand(async () =>
    {
        if (!ServiceProvider.GetRequiredService<IPlatformStuffService>().SupportsMultipleProjects)
            return;

        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64));
    }, "New tab", new CommandShortcut(VirtualKeys.T, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand CloseTab => GetCommand(async () =>
    {
        if (!ServiceProvider.GetRequiredService<IPlatformStuffService>().SupportsMultipleProjects)
            return;

        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().CloseProjectAsync(AppState.CurrentProject);
    }, "Close tab", new CommandShortcut(VirtualKeys.W, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand Save => GetCommand(async () =>
    {
        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().SaveCurrentProjectAsync();
    }, "Save", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand SaveAs => GetCommand(async () =>
    {
        HideMainMenu();
        await ServiceProvider.GetRequiredService<IProjectService>().SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);
    }, "Save As...", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.All);

    public Pix2dCommand ExportImage => GetCommand(() =>
    {
        HideMainMenu();
        AppState.UiState.PreferredExportFormat = ".png";
        ServiceProvider
            .GetRequiredService<ICommandService>()
            .GetCommandList<ViewCommands>()?
            .ShowExportDialogCommand.Execute();
    }, "Export Image...", contextType: EditContextType.All);

    public Pix2dCommand ExportAnimation => GetCommand(async () =>
    {
        HideMainMenu();
        AppState.UiState.PreferredExportFormat = ".gif";
        ServiceProvider
            .GetRequiredService<ICommandService>()
            .GetCommandList<ViewCommands>()?
            .ShowExportDialogCommand.Execute();
        //var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
        //exportVm.SelectExporterByFileType(ExportImportProjectType.Gif);

    }, "Export Animation...", new CommandShortcut(VirtualKeys.E, KeyModifier.Ctrl | KeyModifier.Shift), EditContextType.All);

    public Pix2dCommand Rename => GetCommand(async () =>
    {
        await ServiceProvider.GetRequiredService<IProjectService>().RenameCurrentProjectAsync();
    }, "Rename project", null, EditContextType.All);

    // TODO: Currently doesn't work.
    // public Pix2dCommand SaveToFolder => GetCommand("Save as Folder",
    //     new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Alt | KeyModifier.Shift),
    //     EditContextType.General,
    //     async () =>
    //     {
    //         Commands.View.HideMainMenuCommand.Execute();
    //         await CoreServices.ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2dFolder);
    //     });

    // Reached only via the File menu. Alt+F4 is intentionally NOT bound here
    // because it is also a system shortcut (WM_CLOSE on Windows). When both paths
    // fired together, the menu command kicked off ForceSaveAsync which took the
    // session lock, and MainWindow.Closing's OnAppClosing kicked off a SECOND
    // ForceSaveAsync that blocked on the same semaphore for 5 s and timed out
    // ("Force save timed out after 5 seconds"). With the shortcut removed, Alt+F4
    // goes through the natural OS path — MainWindow.Closing → OnAppClosing →
    // ForceSaveAsync — exactly once, and the in-flight save can complete.
    //
    // Environment.Exit still has to live here because we cannot reach the
    // Avalonia application lifetime from this assembly (Pix2d.Shared has no
    // Avalonia reference). 15 s is enough headroom for a full session flush on
    // large projects.
    public Pix2dCommand Exit => GetCommand(async () =>
    {
        try
        {
            var session = ServiceProvider.GetService<ISessionService>();
            if (session is not null)
                await session.ForceSaveAsync(TimeSpan.FromSeconds(15));
        }
        catch (Exception ex)
        {
            // Best-effort: a save failure must not block the user from exiting.
            Logger.LogException(ex);
        }
        Environment.Exit(0);
    }, "Exit", shortcut: null, EditContextType.All);
}