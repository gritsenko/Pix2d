using System;
using SkiaSharp;

namespace Pix2d.Primitives;

/// <summary>
/// The one invariant every drawable canvas must satisfy: at least one pixel in each dimension.
///
/// <para>A zero-sized sprite is not merely useless, it is actively fatal. <c>BitmapNode</c> cannot
/// allocate a backing bitmap for it (<c>EnsureBitmap</c> throws <c>"Bitmap is null"</c> — the residual
/// throw kept deliberately, since a 0x0 buffer is not recoverable), so pointer-down on such a sprite
/// killed *every* stroke attempt: appstat reported 12 events / 2 users on 3.10.0 with
/// <c>app_context: canvas=0x0</c>, i.e. someone facing an editor they simply could not draw in.
/// Downstream a 0x0 <see cref="SKBitmap"/> also silently poisons diff/undo state, because run-length
/// diffs are sized from the pixel buffer.</para>
///
/// <para>Rather than chase the one upstream producer, the size is clamped at every choke point where a
/// canvas dimension enters the model — creation (<c>Pix2dSprite.CreateEmpty</c>, <c>SpriteNode</c>),
/// mutation (crop / resize) and load (<c>SceneIntegrity</c>) — and the drawing layer refuses to start a
/// stroke on a degenerate target. <see cref="Sanitize(SKSize)"/> only ever clamps *up*, so a legitimate
/// size passes through byte-identical.</para>
/// </summary>
public static class CanvasSize
{
    /// <summary>Smallest canvas dimension the editor will accept, in pixels.</summary>
    public const float MinDimension = 1f;

    /// <summary>
    /// True when the size cannot back a bitmap — zero, negative, or NaN in either dimension.
    /// Written as a negated "is valid" test so NaN (which fails every comparison) reads as degenerate.
    /// </summary>
    public static bool IsDegenerate(SKSize size)
        => !(size.Width >= MinDimension) || !(size.Height >= MinDimension);

    /// <inheritdoc cref="IsDegenerate(SKSize)"/>
    public static bool IsDegenerate(float width, float height)
        => !(width >= MinDimension) || !(height >= MinDimension);

    /// <summary>Clamps each dimension up to <see cref="MinDimension"/>; valid sizes are returned unchanged.</summary>
    public static SKSize Sanitize(SKSize size)
        => IsDegenerate(size) ? new SKSize(Clamp(size.Width), Clamp(size.Height)) : size;

    /// <summary>
    /// Clamps a crop/canvas rectangle so it keeps its origin but is never empty. Used by the crop paths,
    /// which derive bounds from a selection or a drag and can legitimately produce a sub-pixel rect.
    /// </summary>
    public static SKRect Sanitize(SKRect bounds)
        => IsDegenerate(bounds.Width, bounds.Height)
            ? SKRect.Create(bounds.Left, bounds.Top, Clamp(bounds.Width), Clamp(bounds.Height))
            : bounds;

    private static float Clamp(float value)
        => float.IsNaN(value) || value < MinDimension ? MinDimension : value;
}
