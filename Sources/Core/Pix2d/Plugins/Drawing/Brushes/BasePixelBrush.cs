using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Drawing.Brushes
{
    public abstract class BasePixelBrush : IPixelBrush
    {
        protected SKBitmap Preview;
        protected float _opacity = 1f;
        protected float _scale = 1f;
        protected SKPointI _lastPos;
        protected float _cacheSize;
        protected SKColor _cacheColor;
        protected float Spacing { get; set; } = 0.01f;
        public float AbsoluteSpacing { get; set; } = 1;

        protected SKBitmap _brushBitmap;

        public int Size => (int) _scale;
        public float Opacity => _opacity;

        public SKPointI CenterPoint { get; set; }
        public SKPointI BottomRightPoint { get; set; }

        public SKPointI PixelOffset => CenterPoint;
    
        protected void CalculatePoints(float scale)
        {
            var size = (int)Math.Max(1, scale);
            var ds = (int)(0.5 * size);

            var offset = size % 2 == 0 ? 1 : 0;

            CenterPoint = new SKPointI(ds - offset, ds - offset);
            BottomRightPoint = new SKPointI(size - ds + offset, size - ds + offset);
        }

        public abstract SKBitmap GetPreview(float scale);

        public virtual SKBitmap GetBrushBitmap(SKColor color, float scale)
        {
            if (color.Equals(_cacheColor) && Math.Abs(scale - _cacheSize) < 0.1)
                return _brushBitmap;

            _cacheColor = color;
            _cacheSize = scale;

            return null;
        }

        public virtual Task InitBrush(float scale, float opacity, float spacing)
        {
            _scale = scale;
            _opacity = opacity;
            Spacing = spacing;
            CalculatePoints(scale);
            AbsoluteSpacing = Spacing;
            return Task.CompletedTask;
        }


        public virtual bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure,
            bool ignoreSpacing = false)
        {
            //ignoreSpacing = true;
            if (ignoreSpacing)
            {
                DrawCore(layer, pos, color, pressure);
                return true;
            }

            var dst = pos.DistanceTo(_lastPos);
            if (dst >= AbsoluteSpacing)
            {
                //Debug.WriteLine(dst); 
                _lastPos = pos;
                DrawCore(layer, pos, color, pressure); 
                return true;
            }

            return false;
        }

        public virtual bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing)
        {
            if (!ignoreSpacing)
            {
                if (pos.DistanceTo(_lastPos) < AbsoluteSpacing)
                {
                    _lastPos = pos;
                    return false;
                }
                _lastPos = pos;
            }

            EraseCore(layer, pos, pressure);
            return true;
        }

        protected virtual void EraseCore(IDrawingLayer layer, SKPointI pos, double pressure)
        {
            var bm = GetBrushBitmap(SKColors.White, _scale);
            var destRect = GetRect(pos - CenterPoint, new SKSize(bm.Width, bm.Height));
            layer.DrawWithBitmap(bm, destRect, SKBlendMode.DstOut, (float)(_opacity * pressure));
        }

        protected virtual void DrawCore(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure)
        {
            var bm = GetBrushBitmap(color, (float) (_scale * pressure));
            var destRect = GetRect(pos - CenterPoint, new SKSize(bm.Width, bm.Height));
            var composMode = SKBlendMode.SrcOver;

            layer.DrawWithBitmap(bm, destRect, composMode, _opacity);
        }

        private SKRect GetRect(SKPointI pos, SKSize size)
        {
            return new SKRect(pos.X, pos.Y, pos.X + size.Width, pos.Y + size.Height);
        }
    }
}