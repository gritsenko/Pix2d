using System.Reflection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes
{
    public class Pix2dWatermarkNode : TextNode
    {
        private SKBitmap _bitmap;
        private SKImageFilter _shadowFilter;

        public Pix2dWatermarkNode(SKBitmap watermarkBitmap)
        {
            Text = "Pix2d Free";
            _bitmap = watermarkBitmap;
            _shadowFilter = SKImageFilter.CreateDropShadow(0, 0, 3, 3, SKColors.White.WithAlpha((byte) (0.3f * 255)));
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            var zoom = vp.Zoom;

            if (vp.Size.Width >= 200 && vp.Size.Height >= 200)
            {
                using (var p = new SKPaint())
                {
                    p.ImageFilter = _shadowFilter;
                    canvas.DrawBitmap(_bitmap, new SKRect(0, 0, Size.Width, Size.Height), p);
                }
                return;
            }

            var paint = canvas.GetSolidFillPaint(new SKColor(0xFF363d45));

            canvas.DrawRect(0,0,Size.Width, Size.Height, paint);

            if (string.IsNullOrWhiteSpace(Text))
                return;

            if (Size.Width * (14 / FontSize) < 128)
            {
                Text = "Pix2d";
            }

            var textPaint = canvas.GetSolidFillPaint(SKColors.White);
            textPaint.TextSize = FontSize;
            canvas.DrawText(Text, new SKPoint(FontSize/2, FontSize + 1), textPaint);

        }
    }
}