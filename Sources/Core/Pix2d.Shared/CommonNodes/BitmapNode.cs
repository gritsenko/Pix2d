using System;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes
{

    public class BitmapNode : SKNode, IDrawingTarget, IBitmapNode
    {
        private SKBitmap _bitmap;
        protected SKRect _bitmapRect;
        protected SKRect _nodeRect;
        private Func<SKBitmap> _substitute;
        public override bool DoNotRenderChildren => true;

        public SKBitmap Bitmap
        {
            get
            {
                    return _bitmap;
            }
            set
            {
                if (_bitmap != value)
                {
                    _bitmap = value;

                    UpdateSize(value);
                    OnBitmapChanged(_bitmap);
                }

            }
        }

        protected virtual void OnBitmapChanged(SKBitmap newBitmap)
        {
        }

        internal void ResetSizeToSourceBitmap()
        {
            this.Size = new SKSize(this.Bitmap.Width, this.Bitmap.Height);
            OnNodeInvalidated();
        }

        public SKSize SourceSize => new SKSize(_bitmap.Width, _bitmap.Height);

        public void EraseBitmap()
        {
            throw new NotImplementedException();
        }

        public void SetData(byte[] data)
        {
            if (data.Length != Bitmap.ByteCount)
            {
                throw new InvalidOperationException(
                    $"Size of input data {data.Length} is not equal to the size of the bitmap {Bitmap.ByteCount}");
            }
            
            unsafe
            {
                fixed (byte* pSource = data)
                {
                    Buffer.MemoryCopy(pSource, Bitmap.GetPixels().ToPointer(), data.Length, data.Length);
                }
            }
            InvalidateBitmap();
        }

        public void HideTargetBitmap()
        {
            this.IsVisible = false;
        }

        public void ShowTargetBitmap()
        {
            this.IsVisible = true;
        }

        public void SetTargetBitmapSubstitute(Func<SKBitmap> substitute)
        {
            _substitute = substitute;
        }

        public bool IsTargetBitmapVisible()
        {
            return IsVisible;
        }

        public float GetOpacity() => Opacity;
        public SKColor PickColorByPoint(int localPosX, int localPosY)
        {
            return Bitmap.GetPixel(localPosX, localPosY);
        }

        public void Draw(Action<SKCanvas> drawAction)
        {
            var bitmap = Bitmap;

            if (bitmap == null)
                return;

            using (var canvas = new SKCanvas(bitmap))
            {
                drawAction?.Invoke(canvas);
                canvas.Flush();
            }
            bitmap.NotifyPixelsChanged();
        }

        public void ModifyBitmap(Action<SKBitmap> processAction)
        {
            throw new NotImplementedException();
        }

        public SKSize GetSize()
        {
            return Size;
        }

        public void CopyBitmapTo(SKBitmap workingBitmap)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        protected void UpdateSize(SKBitmap value)
        {
            if(value != null)
                Size = new SKSize(value.Width, value.Height);
        }

        protected override void OnSizeChanged()
        {
            if (Bitmap == null)
            {
                Bitmap = new SKBitmap((int) Size.Width, (int) Size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                Bitmap.Erase(SKColor.Empty);
            }
            Bitmap.NotifyPixelsChanged();

            _bitmapRect = new SKRect(0, 0, Bitmap.Width, Bitmap.Height);
            _nodeRect = new SKRect(0, 0, Size.Width, Size.Height);
            base.OnSizeChanged();
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            var bitmap = _substitute == null ? Bitmap : _substitute();
            if (bitmap != null)
            {
                if (Math.Abs(Size.Width - bitmap.Width) < 0.1 && Math.Abs(Size.Height - bitmap.Height) < 0.1)
                {
                    canvas.DrawBitmap(bitmap, 0, 0);
                }
                else
                {
                    canvas.DrawBitmap(bitmap, _bitmapRect, _nodeRect, new SKPaint(){FilterQuality = SKFilterQuality.None});
                }
            }
//            using (var paint = canvas.GetSimpleStrokePaint(1, SKColors.Red))
//            {
//                //canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
//            }
            //            base.OnDraw(canvas, vp);

            //DrawBoundingBox(canvas, vp, 2, SKColors.Purple);
        }

        public void ReplaceBitmap(SKBitmap bitmap, bool resetSize = false)
        {
            _bitmap = bitmap;
            if (resetSize) UpdateSize(_bitmap);
            InvalidateBitmap();
        }

        public virtual void InvalidateBitmap()
        {
            _bitmap?.NotifyPixelsChanged();
            OnNodeInvalidated();
        }

        public void MergeFrom(BitmapNode sprite)
        {
            using (var surface = _bitmap.GetSKSurface())
            {
                var canvas = surface.Canvas;
                canvas.DrawBitmap(sprite.Bitmap, sprite.GetBoundingBox());
                canvas.Flush();
            }

            InvalidateBitmap();
        }

        public byte[] GetData()
        {
            return Bitmap.Bytes;
        }

        //public static BitmapNode CreateEmpty(SKSize size)
        //{
        //    var sprite = new BitmapNode()
        //    {
        //        Bitmap = new SKBitmap(new SKImageInfo((int) size.Width, (int) size.Height, SKColorType.Rgba8888))
        //    };
        //    sprite.Bitmap.Clear();
        //    return sprite;
        //}

        public void RotateSourceBitmap(bool resize = false) 
            => ReplaceBitmap(Bitmap.Rotate90(), resize);

        public void FlipHorizontal() 
            => ReplaceBitmap(Bitmap.FlipHorizontal());

        public void FlipVertical() 
            => ReplaceBitmap(Bitmap.FlipVertical());

        public virtual void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor) 
            => ReplaceBitmap(Bitmap.Resize(newSize.ToSizeI(), horizontalAnchor,verticalAnchor), true);

        public void Crop(SKRect targetBounds) 
            => ReplaceBitmap(Bitmap.Crop(targetBounds), true);

        public Action FlushRequestedAction { get; set; }
        public bool LockTransparentPixels { get; } = false;

        public virtual SKBitmap GetDrawingBitmap()
        {
            return this.Bitmap;
        }

        public override void OnUnload()
        {
            this._bitmap.Dispose();
            this._bitmap = null;
            base.OnUnload();
        }
        //public void SetData(byte[] data, SKSize newSize)
        //{
        //    if (newSize != new SKSize(Bitmap.Width, Bitmap.Height))
        //    {
        //        Bitmap = new SKBitmap(new SKImageInfo((int)newSize.Width, (int)newSize.Height, SKColorType.Rgba8888));
        //    }
        //    unsafe
        //    {
        //        fixed (byte* pSource = data)
        //        {
        //            Buffer.MemoryCopy(pSource, Bitmap.GetPixels().ToPointer(), data.Length, data.Length);
        //        }
        //    }
        //    Bitmap.NotifyPixelsChanged();
        //}

        //public override bool ContainsPoint(SKPoint worldPos)
        //{
        //    if (!base.ContainsPoint(worldPos)) return false;

        //    var localPos = GetLocalPosition(worldPos);

        //    float x = localPos.X;
        //    float y = localPos.Y;

        //    if (Size != SourceSize)
        //    {
        //        x *= SourceSize.Width / Size.Width;
        //        y *= SourceSize.Height / Size.Height;
        //    }

        //    var pixel = Bitmap.PickColorByPoint((int) x, (int) y);

        //    return pixel.Alpha > 0;
        //}

    }
}