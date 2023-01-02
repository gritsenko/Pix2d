using Pix2d.Abstract;
using Pix2d.Primitives;
using Pix2d.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Command
{
    public class SnappingCommands : CommandsListBase
    {
        protected override string BaseName => "View.Snapping";

        public Pix2dCommand ToggleGrid => GetCommand("Toggle grid",
            new CommandShortcut(VirtualKeys.OEMPeriod, KeyModifier.Ctrl),
            EditContextType.General,
            () => CoreServices.SnappingService.ShowGrid = !CoreServices.SnappingService.ShowGrid);

    }
}