using SkiaNodes.Render;
using SkiaSharp;

namespace SkiaNodes.Extensions;

public static class EffectExtensions
{
    /// <summary>
    /// Bakes the effect into the bitmap's pixels, mirroring what <see cref="SKNodeRenderer"/> does at
    /// draw time: the bitmap content plays the role of the node's rendered source surface, the effect
    /// composes itself against that same surface (every <see cref="ISKNodeEffect.Render"/> implementation
    /// self-composes — replace effects overwrite, back/overlay effects re-draw the source as needed),
    /// and the result is read back into the bitmap.
    /// </summary>
    public static void ApplyToBitmap(this ISKNodeEffect effect, SKBitmap targetBitmap)
    {
        var info = new SKImageInfo(targetBitmap.Width, targetBitmap.Height, targetBitmap.ColorType, targetBitmap.AlphaType);
        using var surface = SKSurface.Create(info) ?? SKSurface.Create(new SKImageInfo(targetBitmap.Width, targetBitmap.Height));
        if (surface == null)
            return;

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(targetBitmap, 0, 0);

        var rc = new RenderContext(canvas, new ViewPort(targetBitmap.Width, targetBitmap.Height));
        effect.Render(rc, surface);
        canvas.Flush();

        surface.ReadPixels(targetBitmap.Info, targetBitmap.GetPixels(), targetBitmap.RowBytes, 0, 0);
        targetBitmap.NotifyPixelsChanged();
    }
}
