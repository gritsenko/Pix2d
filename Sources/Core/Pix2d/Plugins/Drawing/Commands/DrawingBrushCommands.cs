using Pix2d.Abstract.Commands;
using Pix2d.Command;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.Drawing.Commands
{
    public class DrawingBrushCommands : CommandsListBase
    {
        protected override string BaseName => "Drawing.Brush";

        public Pix2dCommand DecreaseBrushSize => GetCommand("Decrease brush size",
            new CommandShortcut(VirtualKeys.OEM4),
            EditContextType.Sprite,
            () => CoreServices.DrawingService.ChangeBrushSize(-1));
        public Pix2dCommand IncreaseBrushSize => GetCommand("Increase brush size",
            new CommandShortcut(VirtualKeys.OEM6),
            EditContextType.Sprite,
            () => CoreServices.DrawingService.ChangeBrushSize(1));

    }
}