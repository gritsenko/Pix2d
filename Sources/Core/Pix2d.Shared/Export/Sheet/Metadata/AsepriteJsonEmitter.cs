#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;

namespace Pix2d.Export.Sheet.Metadata;

/// <summary>
/// Emits sprite-sheet metadata in Aseprite's <c>--data</c> JSON shape, so importers written against
/// Aseprite output (Godot, Unity, Phaser, custom engine loaders) read Pix2D sheets unchanged. Frames
/// default to the hash form (Aseprite's default); <see cref="SheetMetadataOptions.ArrayFrames"/> selects
/// the <c>json-array</c> form. Pivot and 9-slice map to <c>meta.slices</c> (Aseprite's own mechanism).
/// </summary>
public sealed class AsepriteJsonEmitter : ISheetMetadataEmitter
{
    public string Id => "aseprite";
    public string DisplayName => "Aseprite JSON";
    public string FileExtension => ".json";

    public string Emit(PackedSheet sheet, SheetMetadataOptions options)
    {
        var info = sheet.Info;
        var doc = new AseDocument
        {
            Meta = new AseMeta
            {
                Version = options.AppVersion,
                Image = info.ImageFileName,
                Size = new AseSize { W = sheet.Image.Width, H = sheet.Image.Height },
                Scale = info.Scale.ToString(CultureInfo.InvariantCulture),
                FrameTags = info.Tags
                    .Select(t => new AseTag { Name = t.Name, From = t.From, To = t.To, Direction = t.Direction, Color = t.Color })
                    .ToList(),
                Slices = BuildSlices(info),
                Layers = options.IncludeLayers && info.Layers.Count > 0
                    ? info.Layers.Select(l => new AseLayer { Name = l.Name, Opacity = l.Opacity, BlendMode = l.BlendMode }).ToList()
                    : null
            }
        };

        // Frame key follows Aseprite's "{title} {frame}" convention; treated as opaque by importers.
        string Key(PackedFrame f) => $"{info.SpriteName} {f.Index}";

        AseFrame ToAse(PackedFrame f, bool withFilename) => new()
        {
            Filename = withFilename ? Key(f) : null,
            Frame = new AseRect { X = f.Frame.Left, Y = f.Frame.Top, W = f.Frame.Width, H = f.Frame.Height },
            Rotated = f.Rotated,
            Trimmed = f.Trimmed,
            SpriteSourceSize = new AseRect
            {
                X = f.SpriteSourceRect.Left,
                Y = f.SpriteSourceRect.Top,
                W = f.SpriteSourceRect.Width,
                H = f.SpriteSourceRect.Height
            },
            SourceSize = new AseSize { W = f.SourceSize.Width, H = f.SourceSize.Height },
            Duration = f.DurationMs
        };

        if (options.ArrayFrames)
            doc.Frames = sheet.Frames.Select(f => ToAse(f, withFilename: true)).ToList();
        else
            doc.Frames = sheet.Frames.ToDictionary(Key, f => ToAse(f, withFilename: false));

        if (options.IncludeAnimationsMap && info.Tags.Count > 0)
        {
            // Aseprite allows multiple tags with the same name; group so a duplicate name doesn't throw
            // (unlike frameTags, a dictionary key must be unique) — union their frame ranges.
            doc.Animations = info.Tags
                .GroupBy(t => t.Name)
                .ToDictionary(
                    g => g.Key,
                    g => sheet.Frames
                        .Where(f => g.Any(t => f.Index >= t.From && f.Index <= t.To))
                        .Select(Key).ToArray());
        }

        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
        return JsonConvert.SerializeObject(doc, settings);
    }

    private static List<AseSlice> BuildSlices(SheetInfo info)
    {
        if (info.Pivot == null && info.NineSlice == null)
            return new List<AseSlice>();

        var key = new AseSliceKey
        {
            Frame = 0,
            Bounds = new AseRect { X = 0, Y = 0, W = info.CanvasSize.Width, H = info.CanvasSize.Height }
        };

        if (info.NineSlice is { } ns)
        {
            key.Center = new AseRect
            {
                X = ns.Left,
                Y = ns.Top,
                W = info.CanvasSize.Width - ns.Left - ns.Right,
                H = info.CanvasSize.Height - ns.Top - ns.Bottom
            };
        }

        if (info.Pivot is { } p)
            key.Pivot = new AsePoint { X = p.X, Y = p.Y };

        return new List<AseSlice>
        {
            new() { Name = info.SpriteName, Color = "#0000ffff", Keys = { key } }
        };
    }
}
