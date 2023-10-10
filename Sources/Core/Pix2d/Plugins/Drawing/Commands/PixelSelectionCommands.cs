using Pix2d.Abstract.Commands;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.Drawing.Commands
{
    public class PixelSelectionCommands : CommandsListBase
    {
        protected override string BaseName => "Edit.Selection";

        public Pix2dCommand SelectAll => GetCommand("Select all", new CommandShortcut(VirtualKeys.A, KeyModifier.Ctrl),
            EditContextType.Sprite,
            () =>
            {
                CoreServices.ToolService.ActivateTool(nameof(PixelSelectTool));
                CoreServices.DrawingService.SelectAll();
                CoreServices.ViewPortService.Refresh();
            });

    }
}