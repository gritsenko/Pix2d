using System.Collections.Generic;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Effects
{
    public class ColorOverlayEffect : BaseEffect
    {
        private SKColor _color = SKColors.Red;
        private float _opacity = 255;
        public override string Name => "Color overlay";
        public override EffectType EffectType { get; } = EffectType.OverlayEffect;

        public float Opacity
        {
            get => _opacity;
            set
            {
                _opacity = value;
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

        public override void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            canvas.DrawColor(_color.WithAlpha((byte) Opacity), SKBlendMode.SrcATop);
        }

        public override ISKNodeEffect Clone()
        {
            return new ColorOverlayEffect();
        }

        public ColorOverlayEffect()
        {
            Order = 1;
//            _settings = new EffectSettings()
//            {
//                new FloatSettingDefition("Opacity", 1f) {Min = 0.0f, Max = 1, Step = 0.01f},
//                new ColorSettingDefition("Color", SKColors.Black)
//            };
        }

        //public override ICanvasImage Process(ICanvasImage source)
        //{
        //    var effect = new ColorSourceEffect();
        //    effect.Color = _settings.GetValue<SKColor>("Color");
            
        //    CompositeEffect compositeEffect = new CompositeEffect();
        //    compositeEffect.Mode = CanvasComposite.SourceAtop;
        //    compositeEffect.Sources.Add(source);
        //    compositeEffect.Sources.Add(effect);

        //    return compositeEffect.ApplyAlpha(_settings.GetValue<float>("Opacity"));
        //}

    }
}