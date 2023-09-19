using System.Diagnostics;
using SkiaSharp;

namespace Pix2d.Drawing
{
    public class DrawingBitmap_ : SKBitmap
    {
        private SKSurface _surface;

        public DrawingBitmap_(int width, int height, SKColorType colorType) : base(width, height, colorType,
            SKAlphaType.Premul)
        {}
        public DrawingBitmap_(int width, int height) : base(width, height, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
        {}

        SKSurface GetSurface()
        {
            if (_surface == null)
                _surface = SKSurface.Create(this.Info, GetPixels(), Width * 4);

            Debug.Assert(_surface != null, "_surface is null");

            return _surface;
        }
        public SKCanvas GetCanvas()
        {
            return GetSurface().Canvas;
        }

        protected override void Dispose(bool disposing)
        {
            _surface?.Dispose();
            base.Dispose(disposing);
        }

        public void Clear()
        {
            this.Erase(SKColor.Empty);
        }

        public void CopyFrom(SKBitmap sourceBitmap)
        {
            using (var canvas = this.GetCanvas())
            {
                canvas.DrawBitmap(sourceBitmap,0,0);
            }
        }
    }
}
