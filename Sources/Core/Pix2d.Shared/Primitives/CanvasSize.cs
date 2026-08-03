using System;
using SkiaSharp;

namespace Pix2d.Primitives;

/// <summary>
/// The invariants every drawable canvas must satisfy: at least one pixel in each dimension, and no
/// dimension so large that its backing bitmap cannot exist.
///
/// <para>A zero-sized sprite is not merely useless, it is actively fatal. <c>BitmapNode</c> cannot
/// allocate a backing bitmap for it (<c>EnsureBitmap</c> throws <c>"Bitmap is null"</c> — the residual
/// throw kept deliberately, since a 0x0 buffer is not recoverable), so pointer-down on such a sprite
/// killed *every* stroke attempt: appstat reported 12 events / 2 users on 3.10.0 with
/// <c>app_context: canvas=0x0</c>, i.e. someone facing an editor they simply could not draw in.
/// Downstream a 0x0 <see cref="SKBitmap"/> also silently poisons diff/undo state, because run-length
/// diffs are sized from the pixel buffer.</para>
///
/// <para>The opposite end is just as fatal, and for the same reason: an absurdly *large* dimension —
/// a typo in the canvas-size panel, which had no bounded input — makes the bitmap allocation throw
/// <c>"Unable to allocate pixels for the bitmap."</c> instead of returning. `Pix2dSprite.Crop` assigns
/// <c>Size</c> before resizing its layers, so the throw left the sprite carrying the impossible size
/// while its layers kept the old pixels, and *every* later operation that re-derives a bitmap from that
/// size (`DrawingLayerNode.SetTarget` allocates three) threw again: appstat reported 21 events / 7 users
/// on 3.11.2, one session with <c>app_context: canvas=64344556x64</c> failing over and over.</para>
///
/// <para>Rather than chase the one upstream producer, the size is clamped at every choke point where a
/// canvas dimension enters the model — creation (<c>Pix2dSprite.CreateEmpty</c>, <c>SpriteNode</c>),
/// mutation (crop / resize) and load (<c>SceneIntegrity</c>) — and the drawing layer refuses to start a
/// stroke on a degenerate target. <see cref="Sanitize(SKSize)"/> only ever clamps *into* the valid
/// range, so a legitimate size passes through byte-identical.</para>
/// </summary>
public static class CanvasSize
{
    /// <summary>Smallest canvas dimension the editor will accept, in pixels.</summary>
    public const float MinDimension = 1f;

    /// <summary>
    /// Largest canvas dimension the editor will accept, in pixels. Chosen so that a square canvas at the
    /// limit still has an addressable pixel buffer: 16384 × 16384 × 4 bytes is exactly
    /// <see cref="int.MaxValue"/> + 1 — i.e. no <c>width * height * 4</c> computed anywhere in the pixel
    /// pipeline can overflow below it. Far beyond any real pixel-art canvas (the largest seen in
    /// telemetry is ~2800 px), so it only ever catches nonsense.
    /// </summary>
    public const float MaxDimension = 16384f;

    /// <summary>
    /// True when the size cannot back a bitmap — zero, negative, or NaN in either dimension.
    /// Written as a negated "is valid" test so NaN (which fails every comparison) reads as degenerate.
    /// </summary>
    public static bool IsDegenerate(SKSize size)
        => !(size.Width >= MinDimension) || !(size.Height >= MinDimension);

    /// <inheritdoc cref="IsDegenerate(SKSize)"/>
    public static bool IsDegenerate(float width, float height)
        => !(width >= MinDimension) || !(height >= MinDimension);

    /// <summary>True when either dimension exceeds <see cref="MaxDimension"/>.</summary>
    public static bool IsOversized(SKSize size) => IsOversized(size.Width, size.Height);

    /// <inheritdoc cref="IsOversized(SKSize)"/>
    public static bool IsOversized(float width, float height)
        => width > MaxDimension || height > MaxDimension;

    /// <summary>
    /// Clamps each dimension into [<see cref="MinDimension"/>, <see cref="MaxDimension"/>]; a valid size
    /// is returned unchanged.
    /// </summary>
    public static SKSize Sanitize(SKSize size)
        => IsDegenerate(size) || IsOversized(size)
            ? new SKSize(Clamp(size.Width), Clamp(size.Height))
            : size;

    /// <summary>
    /// Clamps a crop/canvas rectangle so it keeps its origin but is never empty and never unallocatable.
    /// Used by the crop paths, which derive bounds from a selection or a drag and can legitimately
    /// produce a sub-pixel rect.
    /// </summary>
    public static SKRect Sanitize(SKRect bounds)
        => IsDegenerate(bounds.Width, bounds.Height) || IsOversized(bounds.Width, bounds.Height)
            ? SKRect.Create(bounds.Left, bounds.Top, Clamp(bounds.Width), Clamp(bounds.Height))
            : bounds;

    private static float Clamp(float value)
        => float.IsNaN(value) || value < MinDimension ? MinDimension
            : value > MaxDimension ? MaxDimension
            : value;
}
