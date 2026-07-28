using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ArrangeCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Arrange";

    // Null-safe: the shortcuts are live in the General context whether or not anything is selected.
    public Pix2dCommand SendBackward
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.SendBackward(),
            "Send layer backward", new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl), EditContextType.General);

    public Pix2dCommand BringForward
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>().Selection?.BringForward(),
            "Bring layer forward", new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl), EditContextType.General);

    /// <summary>
    /// Repacks the selected artboards into a dense grid, grouped by their shared name prefixes
    /// ("icon-goal-*" lands in its own row block). One undo step.
    /// </summary>
    public Pix2dCommand Arrange
        => GetCommand(() => ServiceProvider.GetRequiredService<IEditService>().ArrangeSelectedObjects(),
            "Arrange by name", null, EditContextType.General);
}