using System;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using Pix2d.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class FileCommands : CommandsListBase
{
    protected override string BaseName => "File";

    public Pix2dCommand New => GetCommand("New", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl),
        EditContextType.All,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            await CoreServices.ProjectService.CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64));
        });

    public Pix2dCommand Open => GetCommand("Open...", new CommandShortcut(VirtualKeys.O, KeyModifier.Ctrl),
        EditContextType.All,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            await CoreServices.ProjectService.OpenFilesAsync();
        });

    public Pix2dCommand Save => GetCommand("Save", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl),
        EditContextType.All,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            await CoreServices.ProjectService.SaveCurrentProjectAsync();
        });

    public Pix2dCommand SaveAs => GetCommand("Save As...", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Shift),
        EditContextType.All,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            await CoreServices.ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);
        });

    public Pix2dCommand ExportImage => GetCommand("Export Image...", new CommandShortcut(VirtualKeys.E, KeyModifier.Ctrl),
        EditContextType.All,
        () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            AppState.UiState.PreferredExportFormat = ".png";
            Commands.View.ShowExportDialogCommand.Execute();
        });

    public Pix2dCommand ExportAnimation => GetCommand("Export Animation...", new CommandShortcut(VirtualKeys.E, KeyModifier.Ctrl | KeyModifier.Shift),
        EditContextType.All,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            AppState.UiState.PreferredExportFormat = ".gif";
            Commands.View.ShowExportDialogCommand.Execute();
            //var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
            //exportVm.SelectExporterByFileType(ExportImportProjectType.Gif);

        });

    public Pix2dCommand SaveToFolder => GetCommand("Save as Folder",
        new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Alt | KeyModifier.Shift),
        EditContextType.General,
        async () =>
        {
            Commands.View.HideMainMenuCommand.Execute();
            await CoreServices.ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2dFolder);
        });

    public Pix2dCommand Exit => GetCommand("Exit", new CommandShortcut(VirtualKeys.F4, KeyModifier.Alt),
        EditContextType.All,
        () => Environment.Exit(0));

}