#nullable enable
using Avalonia.Threading;
using Pix2d.CommonNodes;
using Pix2d.Project.AutoSave;
using SkiaNodes;
using SkiaNodes.Serialization;
using SkiaSharp;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Builds a fully-detached <see cref="SceneSnapshot"/> on the UI thread without
/// deep-copying any pixel buffers.
///
/// <para>
/// We rely on Skia's copy-on-write contract for <see cref="SKImage.FromBitmap"/>:
/// the returned image shares the source <c>SkPixelRef</c>, so the snapshot is
/// effectively free in both time and memory. The background save thread later
/// calls <c>image.Encode(...)</c>, which is safe because the image is immutable
/// from its consumer's perspective — if the renderer mutates the source bitmap,
/// Skia forks the pixel ref and only the *new* writes go to a fresh buffer; our
/// snapshot keeps reading the original bytes.
/// </para>
///
/// <para>
/// Single rule for invariance: every Skia call inside this method runs on the
/// Avalonia UI thread. Renderer cannot be mutating the same bitmap concurrently,
/// so <see cref="SKImage.FromBitmap"/> always observes a consistent state.
/// </para>
/// </summary>
public sealed class UiThreadSnapshotProvider : ISessionSnapshotProvider
{
    private const float ThumbnailMaxSide = 128f;

    public async Task<SceneSnapshot?> TakeAsync(SKNode? scene, DirtySet dirty, string? sourceProjectPath)
    {
        if (scene is null || dirty.IsEmpty)
            return null;

        // DispatcherOperation<T> is awaitable; we never need .GetTask() — this works
        // identically across Avalonia 11.x and 12.x.
        return await Dispatcher.UIThread.InvokeAsync(
            () => TakeOnUiThread(scene, dirty, sourceProjectPath));
    }

    private static SceneSnapshot TakeOnUiThread(SKNode scene, DirtySet dirty, string? sourceProjectPath)
    {
        var sprite = scene.Nodes.FirstOrDefault() as Pix2dSprite;
        var liveKeys = new List<string>();
        var dirtyFrames = new List<FrameSnapshot>();

        if (sprite is not null)
        {
            var layers = sprite.Layers.ToList();
            CollectLiveKeys(layers, liveKeys);

            // On structural changes we re-snapshot every frame so the store can
            // GC keys that no longer appear. With COW this is still cheap — no
            // pixels are duplicated, just SKImage handles.
            if (dirty.StructureChanged)
                SnapshotAllFrames(layers, dirtyFrames);
            else
                SnapshotDirtyCells(layers, dirty.DirtyCells, dirtyFrames);
        }

        string? sceneJson = null;
        if (dirty.StructureChanged || sprite is null)
        {
            using var serializer = new NodeSerializer();
            sceneJson = serializer.Serialize(scene);
        }

        FrameSnapshot? thumb = null;
        if (sprite is not null && dirty.StructureChanged)
            thumb = TakeThumbnail(sprite);

        return new SceneSnapshot(
            sceneJson: sceneJson,
            structureChanged: dirty.StructureChanged,
            dirtyFrames: dirtyFrames,
            liveFrameKeys: liveKeys,
            thumbnail: thumb,
            sourceProjectPath: sourceProjectPath);
    }

    private static void CollectLiveKeys(List<Pix2dSprite.Layer> layers, List<string> keys)
    {
        foreach (var layer in layers)
            foreach (var node in layer.Nodes.OfType<SpriteNode>())
                keys.Add(node.Id.ToString("N"));
    }

    private static void SnapshotAllFrames(List<Pix2dSprite.Layer> layers, List<FrameSnapshot> output)
    {
        for (var li = 0; li < layers.Count; li++)
        {
            var layer = layers[li];
            for (var fi = 0; fi < layer.FrameCount; fi++)
            {
                if (TrySnapshot(layer.GetSpriteByFrame(fi), li, fi, out var snap))
                    output.Add(snap);
            }
        }
    }

    private static void SnapshotDirtyCells(
        List<Pix2dSprite.Layer> layers,
        IReadOnlySet<(int Layer, int Frame)> cells,
        List<FrameSnapshot> output)
    {
        foreach (var (li, fi) in cells)
        {
            if ((uint)li >= (uint)layers.Count) continue;
            var layer = layers[li];
            if ((uint)fi >= (uint)layer.FrameCount) continue;

            if (TrySnapshot(layer.GetSpriteByFrame(fi), li, fi, out var snap))
                output.Add(snap);
        }
    }

    private static bool TrySnapshot(SpriteNode? sprite, int layerIndex, int frameIndex, out FrameSnapshot snap)
    {
        snap = null!;
        var bitmap = sprite?.Bitmap;
        if (sprite is null || bitmap is null) return false;

        // CRITICAL: SKImage.FromBitmap is allocation-free in the steady state
        // (COW shared pixel ref). We are NOT deep-copying the buffer.
        //
        // If the source bitmap is later mutated by the UI thread, Skia internally
        // forks SkPixelRef so this image keeps observing the unchanged bytes.
        //
        // Lifetime: the SKImage holds a strong ref to the underlying SkPixelRef,
        // so even if the SpriteNode is destroyed mid-save the snapshot stays valid
        // until Disposed by the store.
        var image = SKImage.FromBitmap(bitmap);
        if (image is null) return false;

        snap = new FrameSnapshot(
            key: sprite.Id.ToString("N"),
            layerIndex: layerIndex,
            frameIndex: frameIndex,
            image: image);
        return true;
    }

    private static FrameSnapshot? TakeThumbnail(Pix2dSprite sprite)
    {
        var size = sprite.Size;
        if (size.Width <= 0 || size.Height <= 0) return null;

        var aspect = size.Width / size.Height;
        var scale = aspect > 1 ? ThumbnailMaxSide / size.Width : ThumbnailMaxSide / size.Height;

        // Thumbnails are small (128 px max side ≈ 64 KB) — we render into a
        // fresh bitmap and immediately wrap it in an SKImage. No measurable
        // pressure on the native heap on mobile devices.
        using var bm = new SKBitmap(
            (int)Math.Max(1, size.Width * scale),
            (int)Math.Max(1, size.Height * scale),
            Pix2DAppSettings.ColorType,
            SKAlphaType.Premul);

        var bmRef = bm; // RenderFramePreview takes ref-arg
        sprite.RenderFramePreview(sprite.CurrentFrameIndex, ref bmRef, scale);

        // Mark the bitmap immutable so FromBitmap shares pixels straight away
        // (no internal raster copy even on older Skia builds).
        bmRef.SetImmutable();
        var image = SKImage.FromBitmap(bmRef);
        return image is null ? null : new FrameSnapshot("__thumb__", -1, -1, image);
    }
}
