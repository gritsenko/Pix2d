using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class EffectExtensions
{

    public static void ApplyToBitmap(this ISKNodeEffect effect, SKBitmap targetBitmap)
    {
        using var canvas = new SKCanvas(targetBitmap);
        if (effect.EffectType is EffectType.ReplaceEffect or EffectType.BackEffect)
        {
            canvas.Clear();
        }

        var vp = new ViewPort(targetBitmap.Width, targetBitmap.Height);
        var srcBm = targetBitmap.Copy();
        //Render(canvas, vp, null, srcBm);
        throw new NotImplementedException();

        if (effect.EffectType == EffectType.BackEffect)
        {
            canvas.DrawBitmap(srcBm, 0, 0);
        }
    }

}