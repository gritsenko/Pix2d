using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.UI;
using Pix2d.Primitives.Drawing;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true,
    SettingsViewType = typeof(MagicWandToolSettingsView),
    DisplayName = "Magic wand",
    Group = "Pixel Select",
    HotKey = "M")]
public class PixelSelectColorTool(IDrawingService drawingService, IMessenger messenger, AppState state, IToolService toolService)
    : PixelSelectToolBase(drawingService, messenger, state, toolService)
{
    private IDrawingLayer DrawingLayer => DrawingService.DrawingLayer;

    public int Tolerance
    {
        get => DrawingLayer.ColorSelectionTolerance;
        set => DrawingLayer.ColorSelectionTolerance = Math.Clamp(value, 0, 255);
    }

    public bool SelectWholeLayer
    {
        get => DrawingLayer.ColorSelectionScope == ColorSelectionScope.WholeLayer;
        set => DrawingLayer.ColorSelectionScope = value ? ColorSelectionScope.WholeLayer : ColorSelectionScope.Connected;
    }

    public override Task Activate()
    {
        SelectionMode = PixelSelectionMode.SameColor;
        return base.Activate();
    }
}
