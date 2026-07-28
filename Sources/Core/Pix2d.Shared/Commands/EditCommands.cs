using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Import.Flow;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class EditCommands : CommandsListBase
{
    protected override string BaseName => "Edit";

    // No nested command-list properties here. CommandService only injects ICommandService/IServiceProvider
    // into the lists it registers itself (see CommandService.Initialize), so a `new ArrangeCommands()` held
    // as a property is a *second, uninitialized* instance whose first GetCommand call throws
    // NullReferenceException. Reach a sibling list through ICommandService.GetCommandList<T>() instead.
    // NOTE: ClipboardCommands is currently registered nowhere, so its General-context Ctrl+C/V/X
    // placeholders are not live commands at all.


    //undo redo
    public Pix2dCommand Undo
        => GetCommand(() => ServiceProvider.GetRequiredService<IOperationService>().Undo(),
            "Undo", new CommandShortcut(VirtualKeys.Z, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    public Pix2dCommand Redo
        => GetCommand(() => ServiceProvider.GetRequiredService<IOperationService>().Redo(),
            "Redo", new CommandShortcut(VirtualKeys.Y, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());

    //edit selection
    public Pix2dCommand Delete
        => GetCommand(() => ServiceProvider.GetRequiredService<IEditService>().DeleteSelectedObjectsAsync(),
            "Delete objects", new CommandShortcut(VirtualKeys.Delete), EditContextType.General);

    public Pix2dCommand CancelSelection
        => GetCommand(() =>
        {
            // Esc while a canvas-edit (Resize/Crop) frame is open discards it; otherwise it drops the
            // object selection. Sprite context has its own Esc (SpriteEditCommands.Cancel).
            var canvasEdit = ServiceProvider.GetRequiredService<IArtboardObjectEditService>();
            if (canvasEdit.IsActive)
            {
                canvasEdit.CancelMode();
                return;
            }

            ServiceProvider.GetRequiredService<ISelectionService>().ClearSelection();
        }, "Cancel Selection", new CommandShortcut(VirtualKeys.Escape), EditContextType.General);

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
                var importFlowService = ServiceProvider.GetRequiredService<IImportFlowService>();

                // Allow picking a .pix2d so its sprites can be imported into the current scene.
                var extensions = importService.SupportedExtensions.Append(".pix2d").Distinct().ToArray();
                var files = (await fileService.OpenFileWithDialogAsync(extensions, true, "import")).ToList();
                if (files.Count == 0)
                    return;

                var result = await importFlowService.RunImportFlowAsync(
                    new ImportRequest(files, DropWorldPosition: null, FromDrag: false));

                if (!result.Success)
                {
                    var dialogService = ServiceProvider.GetRequiredService<IDialogService>();
                    dialogService?.ShowAlert(result.Message, "Import error");
                }
            },
            "Import image", new CommandShortcut(VirtualKeys.I, KeyModifier.Ctrl), EditContextType.All, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());
}