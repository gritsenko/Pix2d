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

    public Pix2dCommand SendBackward
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>().Selection.SendBackward(),
            "Send layer backward", new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl), EditContextType.General);

    public Pix2dCommand BringForward
        => GetCommand(() => ServiceProvider.GetRequiredService<ISelectionService>().Selection.BringForward(),
            "Bring layer forward", new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl), EditContextType.General);

}