using System;
using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects
{
    public sealed class ImageAdjustEffect : BaseEffect
    {
        private SKColorFilter _colorFilter;
        private float _hue;
        private float _lightness;
        private float _saturation;

        public ImageAdjustEffect()
        {
            Order = 0;
            UpdateFilter();
        }

        public override string Name => "Image Adjust";
        public override EffectType EffectType { get; } = EffectType.OverlayEffect;

        public float Hue
        {
            get => _hue;
            set
            {
                _hue = value;
                OnSettingsChanged();
            }
        }

        public float Lightness
        {
            get => _lightness;
            set
            {
                _lightness = value;
                OnSettingsChanged();
            }
        }

        public float Saturation
        {
            get => _saturation;
            set
            {
                _saturation = value;
                OnSettingsChanged();
            }
        }

        private void UpdateFilter()
        {
            var colorMatrix = new ColorMatrix();
            
            ApplyHue(colorMatrix, _hue);
            ApplyBrightness(colorMatrix, _lightness);
            ApplySaturation(colorMatrix, _saturation);

            _colorFilter = SKColorFilter.CreateColorMatrix(colorMatrix.GetArray());
        }

        public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            using (var paint = new SKPaint())
            {
                paint.ColorFilter = _colorFilter;
                canvas.DrawBitmap(renderResultBitmap, 0, 0, paint);
            }

            base.Render(canvas, vp, node, renderResultBitmap, nextEffects);
        }

        protected override void OnSettingsChanged()
        {
            UpdateFilter();
            base.OnSettingsChanged();
        }

        public override ISKNodeEffect Clone()
        {
            return new ImageAdjustEffect();
        }

        private void ApplyHue(ColorMatrix matrix, float value)
        {
            value = Math.Clamp(value, -180f, 180f) / 180f * (float)Math.PI;
            if(value == 0)
                return;

            var cosVal = (float)Math.Cos(value);
            var sinVal = (float)Math.Sin(value);
            
            const float lumR = 0.213f;
            const float lumG = 0.715f;
            const float lumB = 0.072f;

            var mat = new[]
            {
                lumR + cosVal * (1 - lumR) + sinVal * (-lumR), lumG + cosVal * (-lumG) + sinVal * (-lumG), lumB + cosVal * (-lumB) + sinVal * (1 - lumB), 0, 0,
                lumR + cosVal * (-lumR) + sinVal * (0.143f), lumG + cosVal * (1 - lumG) + sinVal * (0.140f), lumB + cosVal * (-lumB) + sinVal * (-0.283f), 0, 0,
                lumR + cosVal * (-lumR) + sinVal * (-(1 - lumR)), lumG + cosVal * (-lumG) + sinVal * (lumG), lumB + cosVal * (1 - lumB) + sinVal * (lumB), 0, 0,
                0f, 0f, 0f, 1f, 0f,
                0f, 0f, 0f, 0f, 1f
            };

            matrix.PostConcat(new ColorMatrix(mat));
        }

        private void ApplyBrightness(ColorMatrix matrix, float value)
        {
            value = Math.Clamp(value, -100f, 100f) / 100f;

            if(value == 0)
                return;

            var mat = new[]
            {
                1,0,0,0,value,
                0,1,0,0,value,
                0,0,1,0,value,
                0,0,0,1,0,
                0,0,0,0,1
            };

            matrix.PostConcat(new ColorMatrix(mat));
        }

        private void ApplySaturation(ColorMatrix matrix, float value)
        {
            value = Math.Clamp(value, -100f, 100f);
            if(value == 0)
                return;

            var x = 1 + ((value > 0) ? 3 * value / 100 : value / 100);

            const float lumR = 0.3086f;
            const float lumG = 0.6094f;
            const float lumB = 0.0820f;

            var mat = new[]
            {
                lumR*(1-x)+x,lumG*(1-x),lumB*(1-x),0,0,
                lumR*(1-x),lumG*(1-x)+x,lumB*(1-x),0,0,
                lumR*(1-x),lumG*(1-x),lumB*(1-x)+x,0,0,
                0,0,0,1,0,
                0,0,0,0,1
            };

            matrix.PostConcat(new ColorMatrix(mat));
        }
    }
}
