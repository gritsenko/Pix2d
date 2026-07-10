#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace Pix2d.Services.Palette;

/// <summary>
/// Pure (no-IO) parsing / serialization of external palette formats:
/// GIMP <c>.gpl</c>, flat hex lists (<c>.hex</c>/<c>.txt</c>), PNG swatch strips,
/// and the Lospec palette-list JSON. Extension-driven so the file dialog picks the
/// codec by the chosen filename.
/// </summary>
public static class PaletteFormats
{
    /// <summary>Extensions offered in the Import (open) file dialog.</summary>
    public static readonly string[] ImportExtensions = { ".gpl", ".hex", ".txt", ".png" };

    /// <summary>Extensions offered in the Export (save) file dialog.</summary>
    public static readonly string[] ExportExtensions = { ".gpl", ".hex", ".png" };

    // ---- Import -------------------------------------------------------------

    /// <summary>
    /// Parses palette colors from raw file bytes, selecting the codec by <paramref name="extension"/>
    /// (leading dot, case-insensitive). Returns <c>false</c> on empty / unparseable content.
    /// </summary>
    public static bool TryParse(string? extension, byte[] data, out List<SKColor> colors, out string? name)
    {
        name = null;
        colors = new List<SKColor>();
        var ext = (extension ?? "").ToLowerInvariant();

        try
        {
            colors = ext switch
            {
                ".gpl" => ParseGpl(DecodeText(data), out name),
                ".png" => ParsePng(data),
                _ => ParseHexList(DecodeText(data)) // .hex / .txt / anything else
            };
        }
        catch
        {
            return false;
        }

        return colors.Count > 0;
    }

    public static List<SKColor> ParseGpl(string text, out string? name)
    {
        name = null;
        var result = new List<SKColor>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("GIMP Palette", StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
            {
                name = line.Substring("Name:".Length).Trim();
                continue;
            }

            if (line.StartsWith("Columns:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("#"))
                continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            if (byte.TryParse(parts[0], out var r) &&
                byte.TryParse(parts[1], out var g) &&
                byte.TryParse(parts[2], out var b))
            {
                result.Add(new SKColor(r, g, b));
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a flat list of hex colors. Tolerant: accepts newline / <c>;</c> / <c>,</c> / whitespace
    /// separators, an optional leading <c>#</c>, and both <c>RRGGBB</c> and <c>AARRGGBB</c>.
    /// </summary>
    public static List<SKColor> ParseHexList(string text)
    {
        var result = new List<SKColor>();
        var tokens = text.Split(new[] { '\n', '\r', ';', ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var hex = token.Trim().TrimStart('#');
            if (hex.Length is not (6 or 8))
                continue;

            if (!hex.All(Uri.IsHexDigit))
                continue;

            if (SKColor.TryParse("#" + hex, out var color))
                result.Add(color);
        }

        return result;
    }

    /// <summary>Reads the distinct non-transparent colors of an image in scan order (capped at 1024).</summary>
    public static List<SKColor> ParsePng(byte[] data)
    {
        var result = new List<SKColor>();
        using var bitmap = SKBitmap.Decode(data);
        if (bitmap == null)
            return result;

        var seen = new HashSet<SKColor>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                if (color.Alpha == 0)
                    continue;

                if (seen.Add(color))
                    result.Add(color);

                if (result.Count >= 1024)
                    return result;
            }
        }

        return result;
    }

    public static List<SKColor> ParseLospecJson(string json, out string? name)
    {
        name = null;
        var result = new List<SKColor>();

        var root = JObject.Parse(json);
        name = root.Value<string>("name");

        if (root["colors"] is JArray colors)
        {
            foreach (var token in colors)
            {
                var hex = token.Value<string>();
                if (string.IsNullOrWhiteSpace(hex))
                    continue;

                if (SKColor.TryParse("#" + hex!.TrimStart('#'), out var color))
                    result.Add(color);
            }
        }

        return result;
    }

    // ---- Export -------------------------------------------------------------

    public static string WriteGpl(IReadOnlyList<SKColor> colors, string name)
    {
        var sb = new StringBuilder();
        sb.Append("GIMP Palette\n");
        sb.Append("Name: ").Append(string.IsNullOrWhiteSpace(name) ? "Pix2d Palette" : name).Append('\n');
        sb.Append("Columns: 16\n");
        sb.Append("#\n");

        foreach (var c in colors)
            sb.Append($"{c.Red,3} {c.Green,3} {c.Blue,3}\t#{c.Red:X2}{c.Green:X2}{c.Blue:X2}\n");

        return sb.ToString();
    }

    public static string WriteHexList(IReadOnlyList<SKColor> colors)
    {
        var sb = new StringBuilder();
        foreach (var c in colors)
            sb.Append($"{c.Red:X2}{c.Green:X2}{c.Blue:X2}\n");
        return sb.ToString();
    }

    /// <summary>Renders the palette as a PNG swatch grid (16 columns, 16px cells) — viewable and exactly round-trippable.</summary>
    public static byte[] WritePng(IReadOnlyList<SKColor> colors, int swatchSize = 16, int maxColumns = 16)
    {
        var count = Math.Max(colors.Count, 1);
        var columns = Math.Min(count, maxColumns);
        var rows = (count + columns - 1) / columns;

        using var bitmap = new SKBitmap(columns * swatchSize, rows * swatchSize);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            using var paint = new SKPaint { IsAntialias = false };

            for (var i = 0; i < colors.Count; i++)
            {
                var cx = i % columns * swatchSize;
                var cy = i / columns * swatchSize;
                paint.Color = colors[i];
                canvas.DrawRect(SKRect.Create(cx, cy, swatchSize, swatchSize), paint);
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string DecodeText(byte[] data)
    {
        // Strip a UTF-8 BOM if present so the first line ("GIMP Palette", a hex token) matches.
        return Encoding.UTF8.GetString(data).TrimStart('﻿');
    }
}
