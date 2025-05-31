using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    HasSettings = false, 
    EnabledDuringAnimation = true,
    DisplayName = "Eraser tool",
    HotKey = "E"
    )]
public class EraserTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService) : BrushTool(messenger, drawingService, selectionService)
{
    public override BrushDrawingMode DrawingMode => BrushDrawingMode.Erase;
}