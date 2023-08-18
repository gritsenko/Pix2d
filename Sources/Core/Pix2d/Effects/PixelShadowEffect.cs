using System;
using System.Collections.Generic;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Effects
{
    public class PixelShadowEffect : BaseEffect
    {
        private SKColor _color = SKColors.Black;
        private float _opacity = 200;

        private SKImageFilter _shadowFilter;
        private float _blur = 15;
        private float _deltaX = 3;
        private float _deltaY = 3;
        private float _scale = 100;
        private SKBitmap _cacheBitmap;

        public override string Name => "Shadow";
        public override EffectType EffectType { get; } = EffectType.BackEffect;

        public float Blur
        {
            get => _blur;
            set
            {
                _blur = value;
                OnSettingsChanged();
            }
        }

        public float SigmaX => _blur / 10;
        public float SigmaY => _blur / 10;

        public float DeltaX
        {
            get => _deltaX;
            set
            {
                _deltaX = value;
                OnSettingsChanged();
            }
        }

        public float DeltaY
        {
            get => _deltaY;
            set
            {
                _deltaY = value;
                OnSettingsChanged();
            }
        }

        public SKColor Color
        {
            get => _color;
            set
            {
                _color = value;
                OnSettingsChanged();
            }
        }

        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = value;
                OnSettingsChanged();
            }
        }

        public float Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                OnSettingsChanged();
            }
        }

        public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            if (_shadowFilter == null)
            {
                UpdateShadowFilter();
            }

            RenderCache(renderResultBitmap);

            var rect = new SKRect(0, 0, vp.Size.Width, vp.Size.Height);
            canvas.DrawBitmap(_cacheBitmap, rect);
        }

        private void RenderCache(SKBitmap renderResultBitmap)
        {
            using var paint = new SKPaint();
            paint.ImageFilter = _shadowFilter;

            if (_cacheBitmap == null || _cacheBitmap.Width != renderResultBitmap.Width || _cacheBitmap.Height != renderResultBitmap.Height)
                _cacheBitmap = new SKBitmap(renderResultBitmap.Width, renderResultBitmap.Height);

            using var surface = _cacheBitmap.GetSKSurface();
            surface.Canvas.Clear();
            surface.Canvas.DrawBitmap(renderResultBitmap, 0, 0, paint);
        }

        private void UpdateShadowFilter()
        {
            _shadowFilter = SKImageFilter.CreateDropShadowOnly(DeltaX, DeltaY, SigmaX, SigmaY, Color.WithAlpha((byte)_opacity));
        }

        protected override void OnSettingsChanged()
        {
            UpdateShadowFilter();
            base.OnSettingsChanged();
        }

        public override ISKNodeEffect Clone()
        {
            return new PixelShadowEffect();
        }

        public PixelShadowEffect()
        {
            Order = -1;

            //default settings
            //_settings = new EffectSettings
            //{
            //    new FloatSettingDefition("Opacity", 0.5f) {Min = 0.0f, Max = 1, Step = 0.01f},
            //    new FloatSettingDefition("Blur amount", 1f) {Min = 0.0f, Max = float.PositiveInfinity, Step = 0.1f},
            //    new FloatSettingDefition("Offset", 1f) {Min = 0.0f, Max =  float.PositiveInfinity, Step = 0.1f},
            //    new FloatSettingDefition("Direction", 45) {Min = 0.0f, Max = 360f, Step = 1f},
            //    new ColorSettingDefition("Color", SKColors.Black)
            //};
        }

        //public override IDrawingTarget Process(IDrawingTarget source)
        //{
        //    var effect = new ShadowEffect();
        //    effect.Source = source;
        //    effect.ShadowColor = _settings.GetValue<Color>("Color");
        //    effect.BlurAmount = _settings.GetValue<float>("Blur amount");
        //    var offset = _settings.GetValue<float>("Offset");
        //    var angle = _settings.GetValue<float>("Direction");
        //    var offsetX = (int)(offset*Math.Cos(angle*Math.PI/180));
        //    var offsetY = (int)(offset*Math.Sin(angle*Math.PI/180));

        //    ICanvasImage result = effect;

        //    if (offsetX != 0 || offsetY != 0)
        //    {
        //        var transform = new Transform2DEffect();

        //        transform.Source = effect;
        //        transform.BorderMode = EffectBorderMode.Hard;
        //        transform.InterpolationMode = CanvasImageInterpolation.NearestNeighbor;
        //        transform.TransformMatrix = Matrix3x2.CreateTranslation(offsetX, offsetY);

        //        result = transform;
        //    }

        //    return result.ApplyAlpha(_settings.GetValue<float>("Opacity"));
        //}

    }
}