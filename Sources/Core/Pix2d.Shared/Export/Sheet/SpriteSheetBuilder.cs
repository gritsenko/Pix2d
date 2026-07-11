#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Export.Sheet;

/// <summary>
/// Turns a <see cref="Pix2dSprite"/> into a packed sprite sheet: renders every frame headlessly (CPU
/// Skia, no window), optionally trims each frame, packs them (grid or tight), composites the sheet
/// bitmap, and produces per-frame geometry + animation info. This is the single sheet engine shared by
/// the in-app exporter and the headless CLI.
/// </summary>
public static class SpriteSheetBuilder
{
    public static PackedSheet Build(Pix2dSprite sprite, double scale, SpriteSheetOptions options)
    {
        var frameCount = sprite.GetFramesCount();
        var (from, to) = ResolveFrameRange(sprite, options, frameCount);

        var indexes = new List<int>();
        for (var i = from; i <= to && i < frameCount; i++)
            indexes.Add(i);

        if (indexes.Count == 0)
            return BuildEmpty(sprite, scale, options);

        var renderNodes = new SKNode[] { sprite };

        // Per-frame bitmaps hold native pixel buffers; the outer try/finally disposes them on every
        // exit path (a render/composite throw must not leak them — finalizer-only reclaim is not enough).
        var frameBitmaps = new List<SKBitmap>(indexes.Count);
        try
        {
            // 1. Render each frame at the requested scale (save/restore the live current-frame index).
            var savedFrame = sprite.CurrentFrameIndex;
            try
            {
                foreach (var idx in indexes)
                {
                    sprite.SetFrameIndex(idx);
                    frameBitmaps.Add(renderNodes.RenderToBitmap(SKColor.Empty, scale));
                }
            }
            finally
            {
                sprite.SetFrameIndex(savedFrame);
            }

            var canvasW = frameBitmaps[0].Width;
            var canvasH = frameBitmaps[0].Height;

            // 2. Per-frame source region (trimmed opaque bounds, or the full frame).
            var srcRects = frameBitmaps
                .Select(fb => options.Trim ? FrameTrimmer.GetOpaqueBounds(fb) : new SKRectI(0, 0, fb.Width, fb.Height))
                .ToList();
            var sizes = srcRects.Select(r => new SKSizeI(r.Width, r.Height)).ToList();

            // 3. Pack.
            ISheetPacker packer = options.PackMode == SheetPackMode.Tight
                ? new ShelfSheetPacker()
                : new GridSheetPacker();
            var pack = packer.Pack(sizes, options);

            var sheetSize = options.PowerOfTwo
                ? new SKSizeI(NextPowerOfTwo(pack.Size.Width), NextPowerOfTwo(pack.Size.Height))
                : pack.Size;
            sheetSize = new SKSizeI(Math.Max(1, sheetSize.Width), Math.Max(1, sheetSize.Height));

            // 4. Composite.
            var image = new SKBitmap(sheetSize.Width, sheetSize.Height, SKApp.ColorType, SKAlphaType.Premul);
            var packedFrames = new List<PackedFrame>(indexes.Count);
            using (var canvas = new SKCanvas(image))
            {
                canvas.Clear(SKColor.Empty);
                using var paint = new SKPaint();
                var sampling = new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);

                for (var i = 0; i < indexes.Count; i++)
                {
                    var fb = frameBitmaps[i];
                    var srcRect = srcRects[i];
                    var pos = pack.Positions[i];

                    using (var img = SKImage.FromBitmap(fb))
                    {
                        var src = SKRect.Create(srcRect.Left, srcRect.Top, srcRect.Width, srcRect.Height);
                        var dst = SKRect.Create(pos.X, pos.Y, srcRect.Width, srcRect.Height);
                        canvas.DrawImage(img, src, dst, sampling, paint);
                    }

                    var trimmed = options.Trim &&
                                  !(srcRect.Left == 0 && srcRect.Top == 0 &&
                                    srcRect.Width == canvasW && srcRect.Height == canvasH);

                    packedFrames.Add(new PackedFrame(
                        Index: indexes[i],
                        Frame: new SKRectI(pos.X, pos.Y, pos.X + srcRect.Width, pos.Y + srcRect.Height),
                        Rotated: false,
                        Trimmed: trimmed,
                        SpriteSourceRect: srcRect,
                        SourceSize: new SKSizeI(canvasW, canvasH),
                        DurationMs: GetFrameDurationMs(sprite, indexes[i])));
                }

                canvas.Flush();
            }

            var info = BuildInfo(sprite, scale, options, new SKSizeI(canvasW, canvasH));
            return new PackedSheet { Image = image, Frames = packedFrames, Info = info };
        }
        finally
        {
            foreach (var fb in frameBitmaps)
                fb.Dispose();
        }
    }

    private static PackedSheet BuildEmpty(Pix2dSprite sprite, double scale, SpriteSheetOptions options)
    {
        var image = new SKBitmap(1, 1, SKApp.ColorType, SKAlphaType.Premul);
        var canvasSize = new SKSizeI(
            (int)Math.Max(1, sprite.Size.Width * scale),
            (int)Math.Max(1, sprite.Size.Height * scale));
        return new PackedSheet
        {
            Image = image,
            Frames = Array.Empty<PackedFrame>(),
            Info = BuildInfo(sprite, scale, options, canvasSize)
        };
    }

    private static SheetInfo BuildInfo(Pix2dSprite sprite, double scale, SpriteSheetOptions options, SKSizeI canvasSize)
    {
        var layers = sprite.Layers
            .Select(l => new SheetLayerInfo(l.Name, (int)Math.Round(Math.Clamp(l.Opacity, 0f, 1f) * 255), "normal"))
            .ToList();

        return new SheetInfo(
            SpriteName: options.SpriteName,
            ImageFileName: options.ImageFileName,
            CanvasSize: canvasSize,
            Scale: scale,
            FrameRate: sprite.FrameRate,
            Tags: GetTags(sprite),
            Layers: layers,
            Pivot: GetPivot(sprite, scale),
            NineSlice: GetNineSlice(sprite, scale));
    }

    // --- PR-3 wiring points -----------------------------------------------------------------------
    // The animation-metadata model (named tags, per-frame durations, pivot, 9-slice) does not exist on
    // Pix2dSprite yet (roadmap H2.2 PR-3). Until it lands these return the uniform-timing / no-tag /
    // no-pivot defaults, which still produce valid Aseprite JSON. When the model gains those fields,
    // fill these in and every emitter picks the richer data up automatically.

    private static (int From, int To) ResolveFrameRange(Pix2dSprite sprite, SpriteSheetOptions options, int frameCount)
    {
        // No tags in the model yet → always the full range regardless of TagFilter.
        return (0, Math.Max(0, frameCount - 1));
    }

    private static int GetFrameDurationMs(Pix2dSprite sprite, int frameIndex)
    {
        var fps = Math.Max(1f, sprite.FrameRate);
        return (int)Math.Round(1000f / fps);
    }

    private static IReadOnlyList<SheetTagInfo> GetTags(Pix2dSprite sprite) => Array.Empty<SheetTagInfo>();

    private static SKPointI? GetPivot(Pix2dSprite sprite, double scale) => null;

    private static NineSliceInfo? GetNineSlice(Pix2dSprite sprite, double scale) => null;

    // ----------------------------------------------------------------------------------------------

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        var p = 1;
        while (p < value) p <<= 1;
        return p;
    }
}
