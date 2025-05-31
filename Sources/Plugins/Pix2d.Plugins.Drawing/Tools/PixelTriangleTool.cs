using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools.Shapes;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Triangle",
    Group = "Shapes"
)]
public class PixelTriangleTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService, IViewPortRefreshService viewPortRefreshService) 
    : PixelShapeTool<TriangleShapeBuilder>(messenger, drawingService, selectionService, viewPortRefreshService)
{
}