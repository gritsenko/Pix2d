using Pix2d.Abstract.Drawing;
using Pix2d.CommonNodes;
using Pix2d.Drawing.Brushes;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Drawing.Nodes
{
    public class BrushPreviewNode : SKNode
    {
        private SpriteNode _brushPreview = new SpriteNode();
        private SKPoint _mainPosition;

        public SKPoint MainPosition
        {
            get => _mainPosition;
            set
            {
                _mainPosition = value;
                _brushPreview.Position = value;
            }
        }

        public void UpdatePreview(IPixelBrush brush, SKColor color = default, float scale = 1f)
        {
            if (color == default) 
                color = SKColors.White;

            if(brush == null)
                return;
            
            var bm = ((BasePixelBrush)brush).GetBrushBitmap(color, scale);
            _brushPreview.ReplaceBitmap(bm, true);

            var px = (int)Size.Width / 2;
            if (Size.Width > 1 && (int)Size.Width % 2 == 0) px -= 1;

            var py = (int)Size.Height / 2;
            if (Size.Height > 1 && (int)Size.Height % 2 == 0) py -= 1;
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            canvas.DrawBitmap(_brushPreview.Bitmap, MainPosition.X, MainPosition.Y);
        }
    }

}
