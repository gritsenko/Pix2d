using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Tools;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class EyedropperTool : BaseTool, IDrawingTool
    {
        public IDrawingService DrawingService { get; }
        public override string DisplayName => "Eyedropper tool";

        public override EditContextType EditContextType => EditContextType.Sprite;

        public EyedropperTool(IDrawingService drawingService)
        {
            DrawingService = drawingService;
        }

        public override async Task Activate()
        {
            DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
            await base.Activate();
        }

        protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
            e.Handled = true;
            DrawingService.PickColorByPoint(e.Pointer.WorldPosition);
        }
    }
}