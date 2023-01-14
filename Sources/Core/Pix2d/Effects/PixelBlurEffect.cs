
using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects
{
    public class PixelBlurEffect : BaseEffect
    {
        private float _blurAmount = 15;
        private SKImageFilter _blurFilter;
        public override string Name => "Blur";
        public override EffectType EffectType { get; } = EffectType.ReplaceEffect;

        public float Blur
        {
            get => _blurAmount;
            set
            {
                _blurAmount = value;
                OnSettingsChanged();
            }
        }

        public float SigmaX => _blurAmount / 10;
        public float SigmaY => _blurAmount / 10;

        public float BlurAmount
        {
            get => _blurAmount;
            set
            {
                _blurAmount = value; 
                OnSettingsChanged();
            }
        }

        public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            using (var paint = new SKPaint())
            {
                paint.ImageFilter = _blurFilter;
                canvas.DrawBitmap(renderResultBitmap, 0, 0, paint);
            }
        }

        private void UpdateShadowFilter()
        {
            _blurFilter = SKImageFilter.CreateBlur(SigmaX, SigmaY);
        }

        protected override void OnSettingsChanged()
        {
            UpdateShadowFilter();
            base.OnSettingsChanged();
        }

        public override ISKNodeEffect Clone()
        {
            return new PixelBlurEffect();
        }

        public PixelBlurEffect()
        {
            Order = 0;
            UpdateShadowFilter();
            //default settings
            //_settings = new EffectSettings
            //{
            //    new FloatSettingDefition("Blur amount", 1f) {Min = 0.0f, Max = float.PositiveInfinity, Step = 0.1f},
            //};
        }

//        public override ICanvasImage Process(ICanvasImage source)
//        {
//            var effect = new GaussianBlurEffect
//            {
//                Source = source,
//                BlurAmount = _settings.GetValue<float>("Blur amount")
//            };
//            return effect;
//        }

    }
}