using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using SkiaNodes.Interactive;

namespace Pix2d.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    DisplayName = "Eyedropper tool")]
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