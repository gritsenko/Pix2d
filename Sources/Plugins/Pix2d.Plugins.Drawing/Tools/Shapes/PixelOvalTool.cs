using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Oval",
    Group = "Shapes"
)]
public class PixelOvalTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService, IViewPortRefreshService viewPortRefreshService)
    : PixelShapeTool<OvalShapeBuilder>(messenger, drawingService, selectionService, viewPortRefreshService)
{
}