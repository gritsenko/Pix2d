using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Services;

namespace Pix2d.Drawing.Tools
{
    public class EraserTool : BrushTool
    {
        public override string DisplayName => "Eraser tool";
        public override BrushDrawingMode DrawingMode => BrushDrawingMode.Erase;

        public EraserTool(IDrawingService drawingService, ISelectionService selectionService) : base(drawingService, selectionService)
        {
        }
    }
}