using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using Pix2d.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Command
{
    public class ArrangeCommands : CommandsListBase
    {
        protected override string BaseName => "Edit.Arrange";

        public Pix2dCommand SendBackward
            => GetCommand("Send layer backward",
                new CommandShortcut(VirtualKeys.OEM4, KeyModifier.Ctrl),
                EditContextType.General,
                () => CoreServices.SelectionService.Selection.SendBackward());
        
        public Pix2dCommand BringForward
            => GetCommand("Bring layer forward",
                new CommandShortcut(VirtualKeys.OEM6, KeyModifier.Ctrl),
                EditContextType.General,
                () => CoreServices.SelectionService.Selection.BringForward());

    }
}