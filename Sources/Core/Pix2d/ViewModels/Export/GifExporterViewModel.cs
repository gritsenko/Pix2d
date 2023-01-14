using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Platform;
using Pix2d.Exporters;
using SkiaNodes;

namespace Pix2d.ViewModels.Export
{
    public class GifExporterViewModel : ExporterViewModel
    {
        public GifExporterViewModel(IDialogService dialogService) : base(dialogService)
        {
            Name = "GIF Animation";
        }
        public override async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            var fs = ServiceLocator.Current.GetInstance<IFileService>();
            var node = nodes.FirstOrDefault();
            var file = await fs.GetFileToSaveWithDialogAsync(settings.DefaultFileName ?? "Sprite.gif", new[] { ".gif" }, "export");
            if (file != null)
            {
                using (var stream = await ExportToStream(nodes, settings))
                {
                    await file.SaveAsync(stream);
                }
            }
            else
            {
                throw new OperationCanceledException("Selection file canceled");
            }

        }

        public override Task<Stream> ExportToStream(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            return Task.FromResult(new GifImageExporter().Export(nodes, settings.Scale));
        }

    }
}