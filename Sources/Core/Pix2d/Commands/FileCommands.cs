using System;
using CommonServiceLocator;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.UI;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class FileCommands : CommandsListBase
{
    protected override string BaseName => "File";

    public Pix2dCommand New => GetCommand("New", new CommandShortcut(VirtualKeys.N, KeyModifier.Ctrl),
        EditContextType.All,
        async () => await CoreServices.ProjectService.CreateNewProjectAsync(new SkiaSharp.SKSize(64, 64)));

    public Pix2dCommand Open => GetCommand("Open...", new CommandShortcut(VirtualKeys.O, KeyModifier.Ctrl),
        EditContextType.All,
        async () => await CoreServices.ProjectService.OpenFilesAsync());

    public Pix2dCommand Save => GetCommand("Save", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl),
        EditContextType.All,
        async () => await CoreServices.ProjectService.SaveCurrentProjectAsync());

    public Pix2dCommand SaveAs => GetCommand("Save As...", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Shift),
        EditContextType.All,
        async () => await CoreServices.ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d));

    public Pix2dCommand SaveToFolder => GetCommand("Save as Folder", new CommandShortcut(VirtualKeys.S, KeyModifier.Ctrl | KeyModifier.Alt | KeyModifier.Shift),
        EditContextType.General,
        async () => await CoreServices.ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2dFolder));
        
    public Pix2dCommand Exit => GetCommand("Exit", new CommandShortcut(VirtualKeys.F4, KeyModifier.Alt),
        EditContextType.All,
        () => Environment.Exit(0));
        
    //hidden
    public Pix2dCommand CloseExportDialog =>
        GetCommand("Close export dialog",
            new CommandShortcut(VirtualKeys.Escape),
            EditContextType.All,
            () => ServiceLocator.Current.GetInstance<IMenuController>().ShowExportDialog = false);
}