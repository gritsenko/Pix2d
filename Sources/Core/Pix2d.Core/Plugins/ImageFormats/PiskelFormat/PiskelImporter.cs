#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Platform.FileSystem;
using SkiaSharp;

namespace Pix2d.Plugins.ImageFormats.PiskelFormat;

/// <summary>
/// Imports a Piskel <c>.piskel</c> document (roadmap H2.3) as one sprite: its layers become Pix2d layers,
/// keeping their names and opacity, and its frames become the sprite's animation frames.
///
/// The format's own structure is read by <see cref="PiskelDocument"/>; this class turns it into pixels. Each
/// chunk's <c>base64PNG</c> is a horizontal strip of <c>width</c>-wide cells, and the chunk's
/// <c>layout</c> says which frames each cell fills — so slicing walks the layout, not the cells, which is
/// what makes a sprite with repeated frames come in with its animation intact.
/// </summary>
public class PiskelImporter : IImporter
{
    /// <remarks>
    /// Imports the FIRST file only. A .piskel is a whole sprite, and this interface has one import target, so
    /// there is nowhere to put a second document — the same constraint <c>GifImporter</c> has. It is reached
    /// from File→Open, whose picker allows a multi-select; the drag/drop route goes through
    /// <c>IImportFlowService</c> instead, which handles several documents correctly by making one sprite each.
    /// </remarks>
    public async Task ImportToTargetNode(IEnumerable<IFileContentSource> files, IImportTarget importTarget)
    {
        var file = files.FirstOrDefault()
                   ?? throw new InvalidOperationException("No .piskel file to import.");

        await using var stream = await file.OpenRead();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();

        importTarget.Import(BuildImportData(json));
    }

    /// <summary>
    /// Pure conversion from document text to <see cref="ImportData"/> — no file IO, so a harness can drive
    /// it with a synthesized document.
    /// </summary>
    public static ImportData BuildImportData(string json)
    {
        var doc = PiskelDocument.Parse(json);
        var size = new SKSizeI(doc.Width, doc.Height);

        var data = new ImportData(size, [], replaceFrames: true) { FrameRate = doc.Fps };
        data.Layers.Clear();

        // Piskel's layer 0 is the BOTTOM layer, and so is Pix2d's, so the order carries over unchanged.
        foreach (var layer in doc.Layers)
        {
            var frames = BuildLayerFrames(layer, doc, size);
            data.Layers.Add(new LayerPropertiesInfo
            {
                Name = layer.Name,
                Opacity = Math.Clamp(layer.Opacity, 0f, 1f),
                Frames = frames
            });
        }

        return data;
    }

    private static List<LayerFrameInfo> BuildLayerFrames(PiskelDocument.Layer layer, PiskelDocument.Document doc,
        SKSizeI size)
    {
        // Every layer is padded to the document's frame count: layers may declare different lengths, and a
        // short layer would otherwise leave the sprite's timeline ragged (InsertFrameFromBitmap is per layer).
        var frames = new SKBitmap?[doc.FrameCount];

        foreach (var chunk in layer.Chunks)
        {
            using var sheet = DecodeSheet(chunk.Base64Png);
            if (sheet == null)
                continue;

            for (var cell = 0; cell < chunk.Layout.Count; cell++)
            {
                var cellRect = new SKRectI(
                    cell * size.Width, 0,
                    cell * size.Width + size.Width, size.Height);

                // A layout can name a cell the sheet doesn't actually contain (hand-edited or truncated
                // file). Skip it rather than reading out of bounds — the frame stays transparent.
                if (cellRect.Right > sheet.Width || cellRect.Bottom > sheet.Height)
                    continue;

                SKBitmap? cellBitmap = null;

                foreach (var frameIndex in chunk.Layout[cell])
                {
                    if (frameIndex < 0 || frameIndex >= frames.Length)
                        continue;

                    // Decoded once per cell, then copied per frame it fills: the frames become independent
                    // layer bitmaps that the user edits separately, so they must not share one buffer.
                    cellBitmap ??= ExtractCell(sheet, cellRect);
                    // Two cells (or two chunks) naming the same frame is possible in a hand-edited file;
                    // overwriting without disposing would strand the earlier copy for the finaliser.
                    frames[frameIndex]?.Dispose();
                    frames[frameIndex] = cellBitmap.Copy();
                }

                cellBitmap?.Dispose();
            }
        }

        return frames
            .Select(bitmap =>
            {
                // A frame no layout covers is genuinely empty in Piskel too (that is how it stores a blank
                // frame), so it becomes a transparent frame rather than being dropped — dropping it would
                // shorten the layer and desync it from its siblings.
                var resolved = bitmap ?? CreateEmpty(size);
                return new LayerFrameInfo { BitmapProviderFunc = () => resolved };
            })
            .ToList();
    }

    private static SKBitmap? DecodeSheet(string base64)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        // SKBitmap.Decode hands back the platform-native color type (BGRA on Windows), while the editor
        // works in Pix2DAppSettings.ColorType — ExtractCell normalizes, so the raw decode is fine here.
        return SKBitmap.Decode(bytes);
    }

    private static SKBitmap ExtractCell(SKBitmap sheet, SKRectI cell)
    {
        var info = new SKImageInfo(cell.Width, cell.Height, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        var result = new SKBitmap(info);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(sheet, cell, new SKRect(0, 0, cell.Width, cell.Height));
        return result;
    }

    private static SKBitmap CreateEmpty(SKSizeI size)
    {
        var bitmap = new SKBitmap(new SKImageInfo(size.Width, size.Height, Pix2DAppSettings.ColorType,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        return bitmap;
    }
}
