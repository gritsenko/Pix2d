#nullable enable
using System;
using System.Linq;
using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.Abstract.Import;

/// <summary>
/// Shared logic that populates a freshly created <see cref="Pix2dSprite"/> from <see cref="ImportData"/>:
/// reuses the sprite's existing first layer for the first import layer and adds layers for the rest,
/// inserting frames (normalized to the sprite size). Single source of truth reused by
/// NewSceneFactory and EditService so the layer/frame build logic does not drift.
/// </summary>
public static class SpriteImportApplier
{
    public static void Apply(Pix2dSprite sprite, ImportData data)
    {
        if (data.Layers.Count == 0)
            return;

        var size = data.Size;

        // Formats that record a playback speed must not silently land on the 15 fps default — a .piskel saved
        // at 8 fps would otherwise play almost twice too fast with nothing in the UI hinting why.
        if (data.FrameRate is > 0 and { } frameRate)
            sprite.FrameRate = frameRate;

        for (var i = 0; i < data.Layers.Count; i++)
        {
            var layerInfo = data.Layers[i];
            // The first import layer reuses the sprite's initial layer (created by CreateEmpty);
            // subsequent import layers add new layers.
            var layer = i == 0 ? sprite.Layers.First() : sprite.AddLayer();

            // Layered source formats carry a name and an opacity per layer; flat ones leave the defaults,
            // so this is a no-op for a PNG/GIF import. Applied before frames so a failure mid-import still
            // leaves the layer identifiable.
            if (!string.IsNullOrWhiteSpace(layerInfo.Name))
                layer.Name = layerInfo.Name;
            layer.Opacity = Math.Clamp(layerInfo.Opacity, 0f, 1f);

            if (data.ReplaceFrames)
                ClearFrames(layer);

            for (var frameIndex = 0; frameIndex < layerInfo.Frames.Count; frameIndex++)
            {
                var bitmap = layerInfo.Frames[frameIndex].BitmapProviderFunc?.Invoke();
                if (bitmap == null)
                    continue;

                layer.InsertFrameFromBitmap(frameIndex, NormalizeBitmap(bitmap, size));
            }
        }
    }

    /// <summary>
    /// Drops every existing frame of <paramref name="layer"/>, so the frames that follow are the only ones.
    /// Deleting just frame 0 is not enough: <see cref="Pix2dSprite.AddLayer"/> seeds a new layer with
    /// <c>Layers.First().FrameCount</c> empty frames, and by the time the second import layer is added the
    /// first one already holds all M imported frames. Since <c>InsertFrameFromBitmap</c> *inserts* rather
    /// than overwrites, leaving the surplus behind pushed it to the tail and left the layer with 2M-1
    /// frames — layers disagreeing on FrameCount, with the stale empties saved into the .pix2d.
    /// Only reachable from formats that carry N layers x M frames (.piskel); GIF is 1 layer x M and
    /// import-as-layers is N layers x 1, which is why one DeleteFrame sufficed until now.
    /// </summary>
    public static void ClearFrames(Pix2dSprite.Layer layer)
    {
        while (layer.FrameCount > 0)
            layer.DeleteFrame(layer.FrameCount - 1);
    }

    /// <summary>
    /// Returns a bitmap whose dimensions and color type match the target size, drawing the source at the
    /// top-left (padding with transparency / cropping as needed). <see cref="Pix2dSprite.Layer.InsertFrameFromBitmap"/>
    /// throws when the bitmap size differs from the layer size, so frames of mixed size must be normalized first.
    /// </summary>
    public static SKBitmap NormalizeBitmap(SKBitmap src, SKSizeI targetSize)
    {
        if (src.Width == targetSize.Width
            && src.Height == targetSize.Height
            && src.ColorType == Pix2DAppSettings.ColorType)
        {
            return src;
        }

        var info = new SKImageInfo(targetSize.Width, targetSize.Height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        var dst = new SKBitmap(info);
        using var canvas = new SKCanvas(dst);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(src, 0, 0);
        return dst;
    }
}
