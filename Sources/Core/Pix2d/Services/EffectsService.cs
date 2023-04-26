using System;
using System.Collections.Generic;
using Pix2d.Effects;
using SkiaNodes;

namespace Pix2d.Services;

public class EffectsService : IEffectsService
{
    public static readonly Dictionary<string, Type> AvailableEffects = new Dictionary<string, Type>
    {
        {"Shadow", typeof (PixelShadowEffect)},
        {"Blur", typeof (PixelBlurEffect)},
        {"Color overlay", typeof (ColorOverlayEffect)},
        {"Grayscale", typeof (GrayscaleEffect)},
        {"Image adjust", typeof(ImageAdjustEffect) },
    };

    public Dictionary<string, Type> GetAvailableEffects()
    {
        return AvailableEffects;
    }

    public ISKNodeEffect GetEffect(string name)
    {
        Type effectType = null;

        AvailableEffects.TryGetValue(name, out effectType);

        return (ISKNodeEffect)Activator.CreateInstance(effectType);
    }

}