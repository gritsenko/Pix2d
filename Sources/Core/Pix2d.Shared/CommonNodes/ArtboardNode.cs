using Newtonsoft.Json;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes
{
    public class ArtboardNode : DrawingContainerBaseNode, IClippingSource
    {
        private float _titleWidth;
        private float _titleHeight;
        public string TitleStr => Name + " " + Size.Width + "×" + Size.Height;


        [JsonIgnore]
        public SKNodeClipMode ClipMode => SKNodeClipMode.Rect;
        [JsonIgnore]
        public SKRect ClipBounds => LocalBounds;


        public ArtboardNode()
        {
        }

        public override bool ContainsPoint(SKPoint pos)
        {   
            var titleRect = GetTitleRect();
            if (titleRect.Contains(pos))
            {
                return true;
            }

            if (Nodes.Any())
            {
                return false;
            }

            return base.ContainsPoint(pos);
        }

        private SKRect GetTitleRect()
        {
            return new SKRect(this.Position.X, this.Position.Y - _titleHeight, this.Position.X + _titleWidth, this.Position.Y);
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            //render checkerboard if enabled
            if(BackgroundColor == default)
                base.OnDraw(canvas, vp);

            if (vp.Settings.RenderAdorners)
            {
                using (var paint = new SKPaint() {Color = SKColors.Gray})
                {
                    paint.TextSize = vp.PixelsToWorld(14);
                    canvas.DrawText(TitleStr, 0, -vp.PixelsToWorld(16), paint);
                    _titleWidth = paint.MeasureText(this.TitleStr);
                    _titleHeight = paint.TextSize + vp.PixelsToWorld(16);
                }

                if (BackgroundColor != default)
                {
                    using (var paint = canvas.GetSolidFillPaint(BackgroundColor))
                    {
                        canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
                    }
                }
            }
        }

        public override void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        {
            this.Size = newSize;

            foreach (var node in Nodes)
            {
                var w = node.Size.Width;
                var h = node.Size.Height;
                if (node.Size.Width < this.Size.Width)
                {
                    w = this.Size.Width;
                }

                if (node.Size.Height< this.Size.Height)
                {
                    h = this.Size.Height;
                }
            }

            OnNodeInvalidated();
        }
    }
}