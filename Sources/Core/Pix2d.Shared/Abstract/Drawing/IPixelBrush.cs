using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

/// <summary>
/// How the drawing layer rasterizes a brush's freehand stroke.
/// </summary>
public enum BrushStrokeStyle
{
    /// <summary>Hard pixel-art brush: integer Bresenham + per-dab opacity, no smoothing. Legacy behavior.</summary>
    Pixel,

    /// <summary>
    /// Soft airbrush/spray: streamlined path with even sub-pixel dab spacing, dabs deposited at the per-dab
    /// opacity so overlapping passes <b>build up</b> density (going over an area again darkens it).
    /// </summary>
    Airbrush,

    /// <summary>
    /// Soft marker/pen: same streamlined, evenly-spaced path as <see cref="Airbrush"/>, but the dabs are
    /// unioned into a stroke buffer at full strength and the whole stroke is laid down <b>once</b> at the
    /// brush opacity — so a single stroke stays a flat, even tone no matter how the dabs overlap.
    /// </summary>
    Marker,
}

public interface IPixelBrush
{
    SKPointI PixelOffset { get; }

    SKBitmap GetPreviewBitmap(float scale);
    SKSurface? GetPreviewSurface(SKColor color, float scale);

    int Size { get; }
    float Opacity { get; }

    /// <summary>
    /// How the drawing layer rasterizes this brush's freehand stroke. Hard pixel brushes use
    /// <see cref="BrushStrokeStyle.Pixel"/> (pixel-for-pixel identical to legacy); soft brushes opt into
    /// <see cref="BrushStrokeStyle.Airbrush"/> (build-up spray) or <see cref="BrushStrokeStyle.Marker"/>
    /// (even-opacity pen).
    /// </summary>
    BrushStrokeStyle StrokeStyle { get; }

    /// <summary>When true, the live stylus <see cref="CurrentPressure"/> scales the stamp size.</summary>
    bool PressureAffectsSize { get; set; }

    /// <summary>When true, the live stylus <see cref="CurrentPressure"/> scales the stamp opacity.</summary>
    bool PressureAffectsOpacity { get; set; }

    /// <summary>
    /// Live stylus pressure [0..1] for the stroke currently being drawn. Defaults to <c>1</c> and is
    /// reset to <c>1</c> at the start of every drawing operation, so any path that does not feed real
    /// pressure (shapes, mouse/touch) behaves exactly as before.
    /// </summary>
    double CurrentPressure { get; set; }

    Task InitBrush(float scale, float opacity, float spacing);

    bool Draw(IDrawingLayer layer, SKPointI pos, SKColor color, double pressure, bool ignoreSpacing = false);
    bool Erase(IDrawingLayer layer, SKPointI pos, double pressure, bool ignoreSpacing);
}