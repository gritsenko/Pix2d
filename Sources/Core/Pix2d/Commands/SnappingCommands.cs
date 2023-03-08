using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class SnappingCommands : CommandsListBase
{
    protected override string BaseName => "View.Snapping";

    public Pix2dCommand ToggleGrid => GetCommand("Toggle grid",
        new CommandShortcut(VirtualKeys.OEMPeriod, KeyModifier.Ctrl),
        EditContextType.General,
        () => CoreServices.SnappingService.ShowGrid = !CoreServices.SnappingService.ShowGrid);

}