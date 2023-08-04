using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using Pix2d.Common.Gif;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Exporters;

public class GifImageExporter : SKNodeExporterBase, IFilePickerExporter
{
    public override string Title => "GIF animation";

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
        var fs = ServiceLocator.Current.GetInstance<IFileService>();
        var node = nodes.FirstOrDefault();
        var DefaultFileName = "aaaa!!!!";
        var file = await fs.GetFileToSaveWithDialogAsync(new[] { ".gif" }, "export");
        if (file != null)
        {
            await using var stream = await ExportToStreamAsync(nodes);
            await file.SaveAsync(stream);
        }
        else
        {
            throw new OperationCanceledException("Selection file canceled");
        }

    }
}