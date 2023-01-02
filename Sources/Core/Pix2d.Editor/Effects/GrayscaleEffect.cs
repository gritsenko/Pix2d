using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects
{
    public class GrayscaleEffect : BaseEffect
    {
        private SKColorFilter _filter;
        public override string Name => "Grayscale";
        public override EffectType EffectType { get; } = EffectType.ReplaceEffect;

        public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            using (var paint = new SKPaint())
            {
                paint.ColorFilter = _filter;
                canvas.DrawBitmap(renderResultBitmap, 0, 0, paint);
            }
        }

        private void UpdateFilter()
        {
            _filter = SKColorFilter.CreateColorMatrix(new float[]
                {
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0.21f, 0.72f, 0.07f, 0, 0,
                    0,     0,     0,     1, 0
                });
        }

        protected override void OnSettingsChanged()
        {
            UpdateFilter();
            base.OnSettingsChanged();
        }

        public override ISKNodeEffect Clone()
        {
            return new GrayscaleEffect();
        }

        public GrayscaleEffect()
        {
            Order = 0;
            UpdateFilter();
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