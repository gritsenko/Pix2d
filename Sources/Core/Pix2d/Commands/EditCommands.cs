using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class EditCommands : CommandsListBase
{
    protected override string BaseName => "Edit";

    public ArrangeCommands Arrange { get; } = new ArrangeCommands();
    public ClipboardCommands Clipboard { get; } = new ClipboardCommands();


    //undo redo
    public Pix2dCommand Undo
        => GetCommand("Undo",
            new CommandShortcut(VirtualKeys.Z, KeyModifier.Ctrl),
            EditContextType.All,
            () => CoreServices.OperationService.Undo());

    public Pix2dCommand Redo
        => GetCommand("Redo",
            new CommandShortcut(VirtualKeys.Y, KeyModifier.Ctrl),
            EditContextType.All,
            () => CoreServices.OperationService.Redo());

    //edit selection
    public Pix2dCommand Delete
        => GetCommand("Delete",
            new CommandShortcut(VirtualKeys.Delete),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.Delete());

    public Pix2dCommand DuplicateSelection
        => GetCommand("Duplicate",
            new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.Duplicate());

    public Pix2dCommand MoveLeft =>
        GetCommand("Move left",
            new CommandShortcut(VirtualKeys.Left),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.MoveBy(-1, 0));

    public Pix2dCommand MoveRight =>
        GetCommand("Move right",
            new CommandShortcut(VirtualKeys.Right),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.MoveBy(1, 0));

    public Pix2dCommand MoveUp =>
        GetCommand("Move up",
            new CommandShortcut(VirtualKeys.Up),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.MoveBy(0, -1));

    public Pix2dCommand MoveDown
        => GetCommand("Move down",
            new CommandShortcut(VirtualKeys.Down),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.MoveBy(0, 1));
        
    public Pix2dCommand Hide
        => GetCommand("Hide selected items",
            new CommandShortcut(VirtualKeys.H, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.SelectionService.Selection?.Hide());

    public Pix2dCommand Group
        => GetCommand("Group selected items",
            new CommandShortcut(VirtualKeys.G, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.EditService.GroupNodes(CoreServices.SelectionService.Selection?.Nodes));
    public Pix2dCommand Ungroup
        => GetCommand("Ungroup selected group",
            new CommandShortcut(VirtualKeys.U, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.EditService.UngroupNodes(CoreServices.SelectionService.Selection?.Nodes[0] as GroupNode));

    public Pix2dCommand Import
        => GetCommand("Import image",
            new CommandShortcut(VirtualKeys.I, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.ImportService.ImportToScene());

}