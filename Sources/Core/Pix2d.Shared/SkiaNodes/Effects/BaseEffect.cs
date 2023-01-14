using System;
using System.Collections.Generic;
using SkiaSharp;

namespace SkiaNodes
{
    public abstract class BaseEffect : ISKNodeEffect
    {

        public abstract string Name { get; }
        public abstract EffectType EffectType { get; }
        public int Order { get; set; } = 0;
        public EffectSettings Settings { get; set; }
        public event EventHandler SettingsChanged;

        public virtual void Render(SKCanvas canvas, ViewPort vp, SKNode node, SKBitmap renderResultBitmap, Queue<ISKNodeEffect> nextEffects)
        {
            //var cache = node.GetRenderCacheBitmap();
        }

        public abstract ISKNodeEffect Clone();
        
        public virtual void ApplyToBitmap(SKBitmap targetBitmap)
        {
            var srcBm = targetBitmap.Copy();
            using (var canvas = new SKCanvas(targetBitmap))
            {
                if (EffectType == EffectType.ReplaceEffect || EffectType == EffectType.BackEffect)
                {
                    canvas.Clear();
                }

                Render(canvas, null, null, srcBm, null);

                if (EffectType == EffectType.BackEffect)
                {
                    canvas.DrawBitmap(srcBm, 0, 0);
                }

            }
        }

        protected virtual void OnSettingsChanged()
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}