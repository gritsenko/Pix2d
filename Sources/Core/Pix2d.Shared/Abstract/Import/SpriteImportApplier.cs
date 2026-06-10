#nullable enable
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

        for (var i = 0; i < data.Layers.Count; i++)
        {
            var layerInfo = data.Layers[i];
            // The first import layer reuses the sprite's initial layer (created by CreateEmpty);
            // subsequent import layers add new layers.
            var layer = i == 0 ? sprite.Layers.First() : sprite.AddLayer();

            if (data.ReplaceFrames)
                layer.DeleteFrame(0);

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
