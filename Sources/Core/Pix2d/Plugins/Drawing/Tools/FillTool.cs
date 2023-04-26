using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Brushes;
using Pix2d.Tools;

namespace Pix2d.Drawing.Tools
{
    public class FillTool : BaseTool, IDrawingTool
    {
        public IDrawingService DrawingService { get; }
        private readonly IPixelBrush _previewBrush = new SquareSolidBrush();

        public virtual BrushDrawingMode DrawingMode => BrushDrawingMode.Fill;

        public override EditContextType EditContextType => EditContextType.Sprite;
        public override string DisplayName => "Fill tool";

        public FillTool(IDrawingService drawingService)
        {
            DrawingService = drawingService;
        }

        public override async Task Activate()
        {
            await base.Activate();
            try
            {
                DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                throw;
            }
        }
    }
}
