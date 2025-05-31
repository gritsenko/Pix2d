using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class EditCommands : CommandsListBase
{
    protected override string BaseName => "Edit";

    public ArrangeCommands Arrange { get; } = new ArrangeCommands();
    public ClipboardCommands Clipboard { get; } = new ClipboardCommands();


    //undo redo
    public Pix2dCommand Undo
        => GetCommand(() => ServiceProvider.GetRequiredService<IOperationService>().Undo(),
            "Undo", new CommandShortcut(VirtualKeys.Z, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Redo
        => GetCommand(() => ServiceProvider.GetRequiredService<IOperationService>().Redo(),
            "Redo", new CommandShortcut(VirtualKeys.Y, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    //edit selection
    public Pix2dCommand Delete
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.Delete(), "Delete", new CommandShortcut(VirtualKeys.Delete), EditContextType.General);

    public Pix2dCommand CancelSelection
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>(), "Cancel Selection", new CommandShortcut(VirtualKeys.Escape), EditContextType.General);

    //public Pix2dCommand DuplicateSelection
    //    => GetCommand("Duplicate",
    //        new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.Duplicate());

    //public Pix2dCommand MoveLeft =>
    //    GetCommand("Move left",
    //        new CommandShortcut(VirtualKeys.Left),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.MoveBy(-1, 0));

    //public Pix2dCommand MoveRight =>
    //    GetCommand("Move right",
    //        new CommandShortcut(VirtualKeys.Right),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.MoveBy(1, 0));

    //public Pix2dCommand MoveUp =>
    //    GetCommand("Move up",
    //        new CommandShortcut(VirtualKeys.Up),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.MoveBy(0, -1));

    //public Pix2dCommand MoveDown
    //    => GetCommand("Move down",
    //        new CommandShortcut(VirtualKeys.Down),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.MoveBy(0, 1));
        
    //public Pix2dCommand Hide
    //    => GetCommand("Hide selected items",
    //        new CommandShortcut(VirtualKeys.H, KeyModifier.Ctrl),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.Hide());

    //public Pix2dCommand Group
    //    => GetCommand("Group selected items",
    //        new CommandShortcut(VirtualKeys.G, KeyModifier.Ctrl),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<IEditService>().GroupNodes(ServiceProvider.GetRequiredService<ISelectionService>().Selection?.Nodes));
    //public Pix2dCommand Ungroup
    //    => GetCommand("Ungroup selected group",
    //        new CommandShortcut(VirtualKeys.U, KeyModifier.Ctrl),
    //        EditContextType.General,
    //        () => ServiceProvider.GetRequiredService<IEditService>().UngroupNodes(ServiceProvider.GetRequiredService<ISelectionService>().Selection?.Nodes[0] as GroupNode));

    public Pix2dCommand Import
        => GetCommand(async () =>
            {
                var importService = ServiceProvider.GetRequiredService<IImportService>();
                var fileService = ServiceProvider.GetRequiredService<IFileService>();

                var extensions = importService.SupportedExtensions;
                var files = await fileService.OpenFileWithDialogAsync(extensions.ToArray(), true, "import");

                if (AppState.CurrentProject.CurrentNodeEditor is not IImportTarget importTarget)
                {
                    throw new ArgumentException("Import target is required");
                }

                var result = await importService.ImportAsync(files, importTarget);
                if (!result.Success)
                {
                    var dialogService = ServiceProvider.GetRequiredService<IDialogService>();
                    dialogService?.ShowAlert(result.Message, "Import error");
                }
            },
            "Import image", new CommandShortcut(VirtualKeys.I, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());
}