using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(HasSettings = false)]
public class EraserTool : BrushTool
{
    public static ToolSettings ToolSettings { get; } = new()
    {
        DisplayName = "Eraser tool",
        HotKey = null,
    };

    public override string DisplayName => ToolSettings.DisplayName;
    public override BrushDrawingMode DrawingMode => BrushDrawingMode.Erase;

    public EraserTool(IDrawingService drawingService, ISelectionService selectionService) : base(drawingService, selectionService)
    {
    }
}