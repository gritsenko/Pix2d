using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes
{
    public class SpriteSelectionNode : BitmapNode
    {
        public int MaxRotSpriteEdgeSize = 256;
        private SKBitmap _upscaledBitmap;
        public SKPath SelectionPath { get; set; }

        public void Clear()
        {
            Bitmap?.Clear();
            _upscaledBitmap?.Clear();
            SelectionPath = null;
        }

        protected override void OnBitmapChanged(SKBitmap newBitmap)
        {
            UpdateScaledBitmap(newBitmap);
        }

        private void UpdateScaledBitmap(SKBitmap newBitmap)
        {
            _upscaledBitmap?.Dispose();

            //don't apply rotsprite for big sprites
            if (newBitmap.Width > MaxRotSpriteEdgeSize && newBitmap.Height > MaxRotSpriteEdgeSize)
            {
                _upscaledBitmap = newBitmap;
            }
            else
            {
                _upscaledBitmap = newBitmap.Scale6x();
            }
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            if (Rotation % 90 == 0 || Size.Width > MaxRotSpriteEdgeSize || Size.Height > MaxRotSpriteEdgeSize || _upscaledBitmap == null)
            {
                base.OnDraw(canvas, vp);
            }
            else
            {
                var rect = new SKRect(0, 0, _upscaledBitmap.Width, _upscaledBitmap.Height);
                canvas.DrawBitmap(_upscaledBitmap, rect, _nodeRect);
            }
        }
    }
}