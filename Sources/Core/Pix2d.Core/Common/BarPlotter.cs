using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace Pix2d.Avalonia.Common
{
    public class BarPlotter : UserControl
    {
        private readonly Timer _timer;
        private RenderTargetBitmap _renderTarget;
        private ISkiaDrawingContextImpl _skiaContext;
        private bool _isInitialized;
        private Random _rnd = new Random();
        public List<float> Items { get; set; } = new List<float>();
        public int MaxItems { get; set; } = 100;

        public BarPlotter()
        {
            _timer = new Timer(OnTick, this, 100, 100);
            InitializeCanvas();
        }

        protected override void ArrangeCore(Rect finalRect)
        {
            base.ArrangeCore(finalRect);

            if (Design.IsDesignMode) return;

            if (_renderTarget == null
                || Math.Abs(finalRect.Size.Width - _renderTarget.Size.Width) > 0
                || Math.Abs(finalRect.Size.Height - _renderTarget.Size.Height) > 0)
                InitializeCanvas();
        }

        private void OnTick(object? state)
        {
            Items.Add(_rnd.NextSingle());
            
            if(Items.Count > MaxItems)
                Items.RemoveAt(0);

            Dispatcher.UIThread.Post(InvalidateVisual);
        }

        private void InitializeCanvas()
        {
            if (Bounds.Width <= 1 || Bounds.Height <= 1)
                return;

            var skBrush = new SKPaint();
            skBrush.IsAntialias = true;
            skBrush.Color = new SKColor(0, 0, 0);
            skBrush.Shader = SKShader.CreateColor(skBrush.Color);

            _renderTarget?.Dispose();
            _renderTarget = new RenderTargetBitmap(new PixelSize((int)Bounds.Width, (int)Bounds.Height), new Vector(96, 96));

            _skiaContext = _renderTarget.CreateDrawingContext(null) as ISkiaDrawingContextImpl;
            _skiaContext.SkCanvas.Clear(new SKColor(255, 255, 255));

            _isInitialized = true;
        }


        public override void Render(DrawingContext context)
        {
            if (Design.IsDesignMode || !_isInitialized)
            {
                base.Render(context);
                return;
            }

            RenderBar(_skiaContext.SkCanvas);

            context.DrawImage(_renderTarget,
                new Rect(0, 0, _renderTarget.PixelSize.Width, _renderTarget.PixelSize.Height),
                new Rect(0, 0, Bounds.Width, Bounds.Height)
            );

        }

        private void RenderBar(SKCanvas canvas)
        {
            canvas.Clear(new SKColor(200, 200, 200));

            var paint = new SKPaint() {Color = SKColor.Parse("#f00"), IsStroke = false};

            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[^(i+1)];

                var width = (float) (Bounds.Width / MaxItems);
                var height = (float) (item * Bounds.Height);

                var xoffset = (float) (Bounds.Width - i * width);
                var yoffset = (float) (Bounds.Height - height);

                canvas.DrawRect(xoffset, yoffset, width, height, paint);
            }
        }
    }
}
