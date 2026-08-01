using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

/// <summary>
/// The "transparent canvas" checkerboard every drawing container paints behind its pixels
/// (<see cref="DrawingContainerBaseNode.OnDraw"/>). Extracted so an overlay that temporarily stands in for a
/// canvas — the artboard Resize preview in <c>ArtboardObjectEditorNode</c> — draws the exact same background
/// instead of an approximation of it: the cell size is adaptive (a share of the current zoom), so any
/// hand-rolled copy would drift out of step with the canvas next to it.
/// </summary>
public static class CanvasCheckerboard
{
    private static readonly SKColor Dark = new(0xffd2d2d2);
    private static readonly SKColor Bright = new(0xffffffff);

    private static readonly SKBitmap Pattern = new(2, 2, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
    {
        Pixels = [Bright, Dark, Dark, Bright]
    };

    /// <summary>Fills <paramref name="rect"/> (world coordinates, canvas already under the viewport
    /// transform) with the zoom-adaptive checkerboard.</summary>
    public static void Draw(SKCanvas canvas, ViewPort vp, SKRect rect)
    {
        var cellSize = GridUtils.CalculateAdaptiveStep(vp.DpiEffectiveZoom);
        using var paint = new SKPaint
        {
            Shader = SKShader.CreateBitmap(
                Pattern,
                SKShaderTileMode.Repeat,
                SKShaderTileMode.Repeat,
                SKMatrix.CreateScale(cellSize, cellSize)
            )
        };

        canvas.DrawRect(rect, paint);
    }
}
