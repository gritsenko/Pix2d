using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;

namespace Pix2d.Plugins.Drawing.Commands;

public class PixelSelectionCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Selection";

    public Pix2dCommand SelectAll => GetCommand(() =>
    {
        ServiceProvider?.GetRequiredService<IToolService>().ActivateTool<PixelSelectRectTool>();
        ServiceProvider?.GetRequiredService<IDrawingService>().SelectAll();
        ServiceProvider?.GetRequiredService<IViewPortRefreshService>().Refresh();
    }, "Select all", new CommandShortcut(VirtualKeys.A, KeyModifier.Ctrl), EditContextType.Sprite);

    public Pix2dCommand InvertSelection => GetCommand(() =>
    {
        // Do not force-switch tools here: custom selection tools (including AI contour selection)
        // drop the current marquee on deactivation, which turns invert-selection into SelectAll.
        ServiceProvider?.GetRequiredService<IDrawingService>().InvertSelection();
        ServiceProvider?.GetRequiredService<IViewPortRefreshService>().Refresh();
    }, "Invert selection", new CommandShortcut(VirtualKeys.F2), EditContextType.Sprite);

}