#nullable enable
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Common.Gif;
using Pix2d.Exporters;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.ImageFormats.GifFormat.Exporters;

public class GifImageExporter(IFileService fileService) : SKNodeExporterBase, IFilePickerExporter
{
    public override string? Title => "GIF animation";
    public override string[] SupportedExtensions => new[] {".gif"};
    public override string MimeType => "image/gif";

    protected override Stream EncodeFrames(IEnumerable<SKBitmap> frames, float frameRate, double scale)
    {
        var frameDelay = (int) (100 / frameRate);
        //var e = new MyGifEncoder(frameDelay, (int) scale);
        var e = new AnimatedGifEncoder(frameDelay, (int) scale);
        foreach (var frame in frames)
        {
            e.AddFrame(frame);
        }

        e.Encode();
        return e.GetResultStream();
    }

    public override Task ExportAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        return ExportToFileAsync(nodes, scale);
    }

    public async Task ExportToFileAsync(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var result = await fileService.SaveStreamToFileWithDialogAsync(() => ExportToStreamAsync(nodes, scale), [".gif"], "export");

        if (!result)
            throw new OperationCanceledException("Selection file canceled");
    }
}