#nullable enable
using SkiaSharp;

namespace Pix2d.Project.AutoSave;

/// <summary>
/// Thread-safe, immutable snapshot of a single sprite bitmap.
///
/// Holds an <see cref="SKImage"/> created via <see cref="SKImage.FromBitmap"/>.
/// In SkiaSharp 3.x this returns an immutable view that shares the source bitmap's
/// <c>SkPixelRef</c> under <em>copy-on-write</em>: no native memory is allocated at
/// snapshot time. If the renderer later mutates the source bitmap, Skia forks the
/// <c>SkPixelRef</c> and our snapshot keeps pointing at the old pixels — the original
/// bitmap silently re-points at a fresh buffer for further drawing.
///
/// This is essential on iOS / Android where deep-copying every <see cref="SKBitmap"/>
/// every 30 s would blow the native heap.
///
/// The background save pipeline is the only consumer; <see cref="Dispose"/> releases
/// the SKImage handle (and, transitively, the COW pixel ref).
/// </summary>
public sealed class FrameSnapshot : IDisposable
{
    public FrameSnapshot(string key, int layerIndex, int frameIndex, SKImage image)
    {
        Key = key;
        LayerIndex = layerIndex;
        FrameIndex = frameIndex;
        Image = image;
    }

    /// <summary>Stable identity (NodeId of the source SpriteNode) — used as the PNG file name.</summary>
    public string Key { get; }

    public int LayerIndex { get; }

    public int FrameIndex { get; }

    /// <summary>
    /// Immutable, COW-shared view of the source pixels. Safe to read / encode from
    /// any thread — Skia guarantees that subsequent writes to the source bitmap are
    /// served from a forked pixel ref and cannot modify the bytes seen through this image.
    /// </summary>
    public SKImage Image { get; }

    public void Dispose() => Image.Dispose();
}
