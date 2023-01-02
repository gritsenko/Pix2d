using System;
using System.Collections.Generic;
using SkiaSharp;

namespace SkiaNodes
{
    public interface ISKNodeEffect
    {
        string Name { get; }
//        SKNode Process(ICanvasImage source);

        int Order { get; set; }

        EffectSettings Settings { get; }

        EffectType EffectType { get; }

        event EventHandler SettingsChanged;
        //void LoadSettings(EffectSettingDefintion[] settings);
        void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects);
        ISKNodeEffect Clone();
        void ApplyToBitmap(SKBitmap targetBitmap);
    }

    public enum EffectType
    {
        BackEffect = 1,
        ReplaceEffect = 2,
        OverlayEffect = 3,
    }

    public class EffectSettings
    {
    }
}