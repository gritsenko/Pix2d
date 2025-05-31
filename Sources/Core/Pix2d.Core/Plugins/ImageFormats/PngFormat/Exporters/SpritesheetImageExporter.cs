#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Exporters;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.PngFormat.Exporters;

public class SpritesheetImageExporter(IFileService fileService) : SKNodeExporterBase, IFilePickerExporter
{
    public override string? Title => "PNG sprite sheet";
    public int MaxColumns { get; set; } = 4;
    protected override Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale)
    {
        var framesArr = frames.ToArray();

        var w = (int)(framesArr[0].Width * MaxColumns * scale);

        var rows = Math.Ceiling(framesArr.Length / (double)MaxColumns);

        var h = (int)(framesArr[0].Height * rows * scale);

        using var bitmap = new SKBitmap(w, h, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        var vp = new ViewPort(w, h);
        vp.Settings.RenderAdorners = false;
        vp.ShowArea(new SKRect(0, 0, w, h));

        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear();

            var x = 0;
            var y = 0;
            var paint = new SKPaint()
            {
                FilterQuality = SKFilterQuality.None
            };

            foreach (var frame in framesArr)
            {
                var fw = (int)(frame.Width * scale);
                var fh = (int)(frame.Height * scale);

                var destRect = new SKRect(x, y, x + fw, y + fh);
                canvas.DrawBitmap(frame, destRect, paint);

                x += fw;

                if (x < w) continue;

                x = 0;
                y += fh;
            }
            canvas.Flush();
        }
        return bitmap.ToPngStream();
    }

    public override string MimeType => "image/png";

    public override Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        return ExportToFileAsync(nodes, scale);
    }

    public override string[] SupportedExtensions => new[] { ".png" };

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var result = await fileService.SaveStreamToFileWithDialogAsync(() => ExportToStreamAsync(nodes, scale), [".png"], "export");
        if (!result)
            throw new OperationCanceledException("Selection file canceled");
    }
}