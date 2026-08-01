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
                        // Position within the exported sheet, not the source frame index: under a tag
                        // filter the sheet is re-based to 0 (what Aseprite's own --tag export does) and
                        // GetTags re-bases the tag range to match, so the emitter's animations map — which
                        // pairs frame indexes against tag ranges — keeps lining up. Without a filter the
                        // two are identical, since indexes[i] == i.
                        Index: i,
                        Frame: new SKRectI(pos.X, pos.Y, pos.X + srcRect.Width, pos.Y + srcRect.Height),
                        Rotated: false,
                        Trimmed: trimmed,
                        SpriteSourceRect: srcRect,
                        SourceSize: new SKSizeI(canvasW, canvasH),
                        DurationMs: GetFrameDurationMs(sprite, indexes[i])));
                }

                canvas.Flush();
            }

            var info = BuildInfo(sprite, scale, options, new SKSizeI(canvasW, canvasH), frameCount, indexes.Count);
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
            Info = BuildInfo(sprite, scale, options, canvasSize, sprite.GetFramesCount(), exportedFrameCount: 0)
        };
    }

    /// <param name="exportedFrameCount">
    /// Number of frames actually on the sheet — the index space tags are re-based into, which differs
    /// from the sprite's frame count whenever a tag filter is active.
    /// </param>
    private static SheetInfo BuildInfo(
        Pix2dSprite sprite, double scale, SpriteSheetOptions options, SKSizeI canvasSize,
        int spriteFrameCount, int exportedFrameCount)
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
            Tags: GetTags(sprite, options, spriteFrameCount, exportedFrameCount),
            Layers: layers,
            Pivot: GetPivot(sprite, scale),
            NineSlice: GetNineSlice(sprite, scale, canvasSize));
    }

    // --- Animation metadata (roadmap H2.2 PR-3) ----------------------------------------------------
    // These read the model on Pix2dSprite (tags, per-frame durations, export pivot, 9-slice). Each one
    // clamps defensively even though SceneIntegrity normalises on load: a sheet can be built from a
    // sprite the user is mid-edit on, and a malformed range here would produce metadata that breaks an
    // engine importer rather than an obvious error.

    /// <summary>
    /// Frame range to export. With <see cref="SpriteSheetOptions.TagFilter"/> set this is the named
    /// tag's range; the resulting sheet is then re-based so its frame indexes start at 0, matching what
    /// Aseprite's own <c>--tag</c> export produces.
    /// </summary>
    private static (int From, int To) ResolveFrameRange(Pix2dSprite sprite, SpriteSheetOptions options, int frameCount)
    {
        var all = (0, Math.Max(0, frameCount - 1));

        if (string.IsNullOrWhiteSpace(options.TagFilter))
            return all;

        var tag = FindTag(sprite, options.TagFilter!)
                  ?? throw new ArgumentException(
                      $"Animation tag '{options.TagFilter}' not found in sprite '{sprite.Name}'. "
                      + $"Available: {DescribeTags(sprite)}.", nameof(options));

        return (Math.Clamp(tag.From, 0, Math.Max(0, frameCount - 1)),
                Math.Clamp(tag.To, 0, Math.Max(0, frameCount - 1)));
    }

    internal static SpriteAnimationTag? FindTag(Pix2dSprite sprite, string name)
        => sprite.AnimationTags?.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    internal static string DescribeTags(Pix2dSprite sprite)
        => sprite.AnimationTags is { Count: > 0 } tags
            ? string.Join(", ", tags.Select(t => $"'{t.Name}'"))
            : "(none)";

    private static int GetFrameDurationMs(Pix2dSprite sprite, int frameIndex)
        => sprite.GetFrameDurationMs(frameIndex);

    /// <summary>
    /// Tags for <c>meta.frameTags</c>. Under a tag filter only that tag is emitted, re-based to the
    /// exported sheet's index space — the emitter matches <see cref="PackedFrame.Index"/> against these
    /// ranges to build the <c>animations</c> map, so the two must share one index space.
    /// </summary>
    private static IReadOnlyList<SheetTagInfo> GetTags(
        Pix2dSprite sprite, SpriteSheetOptions options, int spriteFrameCount, int exportedFrameCount)
    {
        if (sprite.AnimationTags is not { Count: > 0 } tags)
            return Array.Empty<SheetTagInfo>();

        // Tag ranges are source-frame indexes, so they clamp against the sprite's frame count...
        var lastFrame = Math.Max(0, spriteFrameCount - 1);

        if (!string.IsNullOrWhiteSpace(options.TagFilter))
        {
            var filtered = FindTag(sprite, options.TagFilter!);
            if (filtered == null)
                return Array.Empty<SheetTagInfo>();

            // ...and are then re-based into the exported sheet's index space, which starts at 0.
            var span = Math.Clamp(filtered.To, 0, lastFrame) - Math.Clamp(filtered.From, 0, lastFrame);
            return [new SheetTagInfo(
                filtered.Name, 0, Math.Clamp(span, 0, Math.Max(0, exportedFrameCount - 1)),
                filtered.GetDirectionKey(), null)];
        }

        return tags
            .Where(t => t.From <= lastFrame && t.To >= 0)
            .Select(t => new SheetTagInfo(
                t.Name,
                Math.Clamp(t.From, 0, lastFrame),
                Math.Clamp(t.To, 0, lastFrame),
                t.GetDirectionKey(),
                null))
            .ToList();
    }

    private static SKPointI? GetPivot(Pix2dSprite sprite, double scale)
        => sprite.ExportPivot is { } p
            ? new SKPointI((int)Math.Round(p.X * scale), (int)Math.Round(p.Y * scale))
            : null;

    /// <summary>
    /// 9-slice margins scaled to output pixels. Skipped when the scaled margins would leave no centre
    /// rect: <c>BuildSlices</c> derives <c>center</c> as <c>W - Left - Right</c>, and a zero/negative
    /// one is what makes an engine importer choke rather than fall back.
    /// </summary>
    private static NineSliceInfo? GetNineSlice(Pix2dSprite sprite, double scale, SKSizeI canvasSize)
    {
        if (sprite.NineSlice is not { } ns)
            return null;

        var scaled = new NineSliceInfo(
            (int)Math.Round(ns.Left * scale),
            (int)Math.Round(ns.Top * scale),
            (int)Math.Round(ns.Right * scale),
            (int)Math.Round(ns.Bottom * scale));

        var fits = scaled.Left >= 0 && scaled.Top >= 0 && scaled.Right >= 0 && scaled.Bottom >= 0
                   && scaled.Left + scaled.Right < canvasSize.Width
                   && scaled.Top + scaled.Bottom < canvasSize.Height;

        return fits ? scaled : null;
    }

    // ----------------------------------------------------------------------------------------------

    private static int NextPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        var p = 1;
        while (p < value) p <<= 1;
        return p;
    }
}
