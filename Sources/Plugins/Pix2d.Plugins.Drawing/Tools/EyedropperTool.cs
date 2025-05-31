using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    DisplayName = "Eyedropper tool",
    HotKey = "I")]
public class EyedropperTool : BaseTool, IDrawingTool
{
    public IDrawingService DrawingService { get; }

    public EyedropperTool(IDrawingService drawingService)
    {
        DrawingService = drawingService;
    }

    public override async Task Activate()
    {
        DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
        await base.Activate();
    }

    protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
    {
        e.Handled = true;
        DrawingService.PickColorByPoint(e.Pointer.WorldPosition);
    }
}