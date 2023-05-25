using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Exporters;
using SkiaNodes;

namespace Pix2d.ViewModels.Export;

public class PngExporterViewModel : ExporterViewModel
{
    public PngExporterViewModel(IDialogService dialogService) : base(dialogService)
    {
        Name = "Single PNG image";
    }

    public override async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
    {
        var fs = ServiceLocator.Current.GetInstance<IFileService>();
        var node = nodes.FirstOrDefault();
        var file = await fs.GetFileToSaveWithDialogAsync(settings.DefaultFileName ?? "Sprite.png", new[] { ".png" }, "project");
        if (file != null)
        {
            await ExportImage(file, nodes, settings.Scale);
        }
        else
        {
            throw  new OperationCanceledException("Selection file canceled");
        }

        //var pm = ServiceLocator.Current.GetInstance<ProjectManager>();
        //var result = await pm.ExportToPng(settings);

        //if (result)
        //    await base.Export(settings);
    }

    public override Task<Stream> ExportToStream(IEnumerable<SKNode> nodes, ImageExportSettings settings)
    {
        var pngExporter = new PngImageExporter();
        return Task.FromResult(pngExporter.Export(nodes, settings.Scale));
    }

    private async Task ExportImage(IFileContentSource fileContentSource, IEnumerable<SKNode> nodesToRender, double scale = 1)
    {
        var pngExporter = new PngImageExporter();
        using (var pngStream = pngExporter.Export(nodesToRender, scale))
        {
            await fileContentSource.SaveAsync(pngStream);
        }
    }

}