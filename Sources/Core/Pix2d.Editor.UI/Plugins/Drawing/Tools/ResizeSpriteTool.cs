using System.Threading.Tasks;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Tools;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Drawing.Tools
{
    public class ResizeSpriteTool : BaseTool, IDrawingTool
    {
        public override string DisplayName => "Crop/Resize tool";

        public override EditContextType EditContextType => EditContextType.Sprite;

        public override async Task Activate()
        {
            DrawingService.DrawingLayer.SetDrawingLayerMode(BrushDrawingMode.ExternalDraw);
            await base.Activate();
        }

        protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
            e.Handled = true;
            var col = DrawingService.GetColorByPoint(e.Pointer.WorldPosition);
            if (!col.Equals(SKColor.Empty))
                DrawingService.CurrentColor = col;
        }
    }
}