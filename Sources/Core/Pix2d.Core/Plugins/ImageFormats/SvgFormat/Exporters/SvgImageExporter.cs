﻿#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.ImageFormats.SvgFormat.Exporters;

public class SvgImageExporter(IFileService fileService) : IFilePickerExporter
{
    public string? Title => "SVG image";

    public Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        return ExportToFileAsync(nodes, scale);
    }

    public string[] SupportedExtensions => new[] { ".svg" };
    public string MimeType => "image/svg+xml";

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var result =
            await fileService.SaveStreamToFileWithDialogAsync(() => Task.FromResult(Export(nodes, scale)), [".svg"],
                "export");

        if (!result)
            throw new OperationCanceledException("Selection file canceled");
    }

    public Stream Export(IEnumerable<SKNode> nodesToExport, double scale = 1)
    {
        var skBitmap = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
        var svg = ConvertBitmapToSvg(skBitmap);

        return GenerateStreamFromString(svg);
    }

    private string ConvertBitmapToSvg(SKBitmap bm)
    {
        var colors = GetPointsByColor(bm);
        var paths = ColorsToPaths(colors);
        var output = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 -0.5 " + bm.Width + " " + bm.Height +
                     "\" shape-rendering=\"crispEdges\">\n<metadata>Made with Pix2d https://pix2d.com</metadata>\n" +
                     paths + "</svg>";

        return output;
    }

    private string ColorsToPaths(Dictionary<SKColor, List<SKPointI>> colors)
    {
        string FormatColor(SKColor col) => $"#{col.Red:x2}{col.Green:x2}{col.Blue:x2}";
        string MakePath(string hexColor, string data) => "<path stroke=\"" + hexColor + "\" d=\"" + data + "\" />\n";
        string MakePathData(int x, int y, int width) => "M" + x + " " + y + "h" + width;

        var output = "";

        // Loop through each color to build paths
        foreach (var item in colors)
        {
            var color = item.Key;
            var pixels = item.Value;
            SKPointI curPixel = pixels[0];
            var w = 1;
            var paths = new List<string>();

            // Loops through each color's pixels to optimize paths
            for (var i = 1; i < pixels.Count; i++)
            {
                var pixel = pixels[i];
                if (pixel.Y == curPixel.Y && pixel.X == curPixel.X + w)
                {
                    w++;
                    continue;
                }

                paths.Add(MakePathData(curPixel.X, curPixel.Y, w));
                w = 1;
                curPixel = pixel;
            }

            paths.Add(MakePathData(curPixel.X, curPixel.Y, w)); // Finish last path
            output += MakePath(FormatColor(color), string.Join("", paths));
        }

        return output;
    }

    private Dictionary<SKColor, List<SKPointI>> GetPointsByColor(SKBitmap bm)
    {
        var colors = new Dictionary<SKColor, List<SKPointI>>();
        var pixels = bm.Pixels;
        for (var i = 0; i < pixels.Length; i++)
        {
            var skColor = pixels[i];

            if (skColor == SKColor.Empty)
                continue;

            var y = i / bm.Width;
            var x = i - (y * bm.Width);
            if (!colors.ContainsKey(skColor))
                colors[skColor] = new List<SKPointI>();

            colors[skColor].Add(new SKPointI(x, y));
        }

        return colors;
    }

    public static Stream GenerateStreamFromString(string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }
}