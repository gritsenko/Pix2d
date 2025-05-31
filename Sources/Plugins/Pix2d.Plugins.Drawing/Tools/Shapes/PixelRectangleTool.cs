using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Rectangle",
    Group = "Shapes"
)]
public class PixelRectangleTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService, IViewPortRefreshService viewPortRefreshService)
    : PixelShapeTool<RectangleShapeBuilder>(messenger, drawingService, selectionService, viewPortRefreshService)
{
}