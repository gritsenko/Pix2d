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
        commandService.GetCommandList<ViewCommands>().HideMainMenuCommand.Execute();
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
            .GetCommandList<ViewCommands>()
            .ShowExportDialogCommand.Execute();
    }, "Export Image...", new CommandShortcut(VirtualKeys.E, KeyModifier.Ctrl), EditContextType.All);

    public Pix2dCommand ExportAnimation => GetCommand(async () =>
    {
        HideMainMenu();
        AppState.UiState.PreferredExportFormat = ".gif";
        ServiceProvider
            .GetRequiredService<ICommandService>()
            .GetCommandList<ViewCommands>()
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

    public Pix2dCommand Exit => GetCommand(() => Environment.Exit(0), "Exit", new CommandShortcut(VirtualKeys.F4, KeyModifier.Alt), EditContextType.All);
}