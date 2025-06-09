using SkiaNodes;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d.Effects;

public class PixelShadowEffect : ISKNodeEffect
{
    private SKImageFilter _shadowFilter;
    private SKSurface _effectRenderCache;
    private SKImageInfo _cacheImageInfo;

    public string Name => "Shadow";
    public EffectType EffectType => EffectType.BackEffect;

    public float Blur { get; set; } = 15;

    public float SigmaX => Blur / 10;
    public float SigmaY => Blur / 10;

    public float DeltaX { get; set; } = 3;

    public float DeltaY { get; set; } = 3;

    public SKColor Color { get; set; } = SKColors.Black;

    public float Opacity { get; set; } = 200;

    /// <summary>
    /// Renders the shadow effect.
    /// This method implements a "back effect" by composing the shadow and the original content
    /// in the correct order on a temporary surface before drawing the final result to the destination canvas.
    /// </summary>
    /// <param name="rc">The render context, providing the destination canvas.</param>
    /// <param name="source">The surface containing the original, rendered content of the node.</param>
    public void Render(RenderContext rc, SKSurface source)
    {
        // Recreate the filter if properties have changed.
        if (_shadowFilter == null) Invalidate();

        // Ensure the temporary render cache surface exists and matches the required size.
        // The SKImageInfo should be based on the source surface's dimensions.
        if (_effectRenderCache == null || _cacheImageInfo.Width != source.Canvas.DeviceClipBounds.Width || _cacheImageInfo.Height != source.Canvas.DeviceClipBounds.Height)
        {
            _effectRenderCache?.Dispose();
            _cacheImageInfo = new SKImageInfo(source.Canvas.DeviceClipBounds.Width, source.Canvas.DeviceClipBounds.Height);
            _effectRenderCache = SKSurface.Create(_cacheImageInfo);
        }

        var tempCanvas = _effectRenderCache.Canvas;
        tempCanvas.Clear(SKColors.Transparent);

        // 1. Draw the shadow.
        // Use a paint with the shadow-only filter to draw the original `source` content
        // as a shadow onto our temporary canvas.
        using (var shadowPaint = new SKPaint { ImageFilter = _shadowFilter })
        {
            tempCanvas.DrawSurface(source, 0, 0, shadowPaint);
        }

        // 2. Draw the original content on top of the shadow.
        // Use a default paint to draw the `source` surface again, this time without any effects.
        using (var originalContentPaint = new SKPaint())
        {
            tempCanvas.DrawSurface(source, 0, 0, originalContentPaint);
        }

        // 3. Replace the destination canvas content with our composite image.
        // `rc.Canvas` is the canvas of the `source` surface provided by the renderer.
        var destCanvas = rc.Canvas;
        destCanvas.Clear(SKColors.Transparent); // Clear the original content.
        destCanvas.DrawSurface(_effectRenderCache, 0, 0); // Draw the composite image (shadow + content).
    }

    /// <summary>
    /// Invalidates and recreates the SkiaSharp image filter.
    /// This should be called whenever a shadow property (like color, blur, or offset) changes.
    /// </summary>
    public void Invalidate()
    {
        _shadowFilter?.Dispose();
        _shadowFilter = SKImageFilter.CreateDropShadowOnly(DeltaX, DeltaY, SigmaX, SigmaY, Color.WithAlpha((byte)Opacity));
    }

    public ISKNodeEffect Clone()
    {
        return new PixelShadowEffect
        {
            Blur = this.Blur,
            DeltaX = this.DeltaX,
            DeltaY = this.DeltaY,
            Color = this.Color,
            Opacity = this.Opacity
        };
    }
}
