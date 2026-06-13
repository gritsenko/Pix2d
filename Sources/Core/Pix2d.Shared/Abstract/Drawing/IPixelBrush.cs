using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public interface IPixelBrush
{
    SKPointI PixelOffset { get; }

    SKBitmap GetPreviewBitmap(float scale);
    SKSurface GetPreviewSurface(SKColor color, float scale);

    int Size { get; }
    float Opacity { get; }

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