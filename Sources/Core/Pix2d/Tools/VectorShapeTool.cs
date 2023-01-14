using System;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Tools
{
    public class VectorShapeTool : ObjectCreationTool
    {
        public override string DisplayName => "Vector shape tool";
        public event EventHandler ShapeTypeChanged;

        private ShapeType _shapeType = ShapeType.Rectangle;
        public ShapeType ShapeType
        {
            get => _shapeType;
            set
            {
                if (_shapeType != value)
                {
                    _shapeType = value;
                    ShapeTypeChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        protected override void CreateObjectCore(SKRect destRect)
        {
            if (destRect.Width < 1 || Math.Abs(destRect.Height) < 1)
                destRect = new SKRect(destRect.Left, destRect.Top, destRect.Left + 64, destRect.Top + 64);
            ObjectCreationService.CreateRectangle(destRect);
        }

    }
}