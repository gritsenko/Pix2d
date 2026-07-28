using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using Pix2d.Primitives.Drawing;

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

    public Pix2dCommand Deselect => GetCommand(() =>
    {
        var drawingService = ServiceProvider.GetRequiredService<IDrawingService>();
        var drawingLayer = drawingService.DrawingLayer;
        if (!drawingLayer.HasSelection)
            return;

        if (drawingLayer.SelectionPhase == SelectionPhase.Transforming)
        {
            // Lifted pixels have to land on the canvas as one undoable step before the marquee goes away —
            // otherwise Ctrl+D would silently throw the in-progress transform away. Returning to the marquee
            // tool is what performs that commit (PixelTransformTool.Deactivate → CommitTransformWithUndo,
            // marquee kept in contour mode) and it also gets the user out of a tool that would have nothing
            // left to transform. The ApplySelection below then drops the contour-only marquee, which by
            // definition never touches pixels.
            ServiceProvider.GetRequiredService<IToolService>().ActivateReturnSelectionTool(AppState);
        }

        drawingLayer.ApplySelection();
        ServiceProvider.GetRequiredService<IViewPortRefreshService>().Refresh();
    }, "Deselect", new CommandShortcut(VirtualKeys.D, KeyModifier.Ctrl), EditContextType.Sprite);
}
