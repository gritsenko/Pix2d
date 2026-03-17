using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Line",
    Group = "Shapes",
    HotKey = "L"
)]
public class PixelLineTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService, IViewPortRefreshService viewPortRefreshService) 
    : PixelShapeTool<LineShapeBuilder>(messenger, drawingService, selectionService, viewPortRefreshService)
{
}