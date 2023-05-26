using System;
using System.Collections.Generic;
using System.IO;
using CommonServiceLocator;
using System.Threading.Tasks;
using Pix2d.Abstract.Export;
using Pix2d.Abstract.Platform;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Exporters;

public class PngImageExporter : IStreamExporter, IFilePickerExporter
{
    public string Title { get; } = "Png image exporter";

    public async Task<Stream> ExportToStream(IEnumerable<SKNode> nodesToExport, double scale = 1)
    {
        try
        {
            var skBitmap = nodesToExport.RenderToBitmap(SKColor.Empty, scale);
            return skBitmap.ToPngStream();
        }
        catch (Exception e)
        {
            IoC.Get<IDialogService>().Alert("There's nothing to Export!", "Export");
            Logger.Log(e.Message);
        }

        return null;
    }

    public async Task ExportToFile(IEnumerable<SKNode> nodes, double scale = 1)
    {
        var fs = ServiceLocator.Current.GetInstance<IFileService>();
        var DefaultFileName = "new project.png";
        var file = await fs.GetFileToSaveWithDialogAsync(DefaultFileName ?? "Sprite.png", new[] { ".png" }, "project");
        if (file != null)
        {
            await using var stream = await ExportToStream(nodes, scale);
            await file.SaveAsync(stream);
        }
        else
        {
            throw new OperationCanceledException("Selection file canceled");
        }
    }

}