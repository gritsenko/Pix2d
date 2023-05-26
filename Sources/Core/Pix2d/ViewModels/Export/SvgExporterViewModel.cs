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
    public class SvgExporterViewModel : ExporterViewModel
    {
        public SvgExporterViewModel(IDialogService dialogService) : base(dialogService)
        {
            Name = "SVG (experimental)";
        }

        public override async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            var fs = ServiceLocator.Current.GetInstance<IFileService>();
            var firstNode = nodes.FirstOrDefault();
            var file = await fs.GetFileToSaveWithDialogAsync(settings.DefaultFileName ?? "Sprite.svg", new[] { ".svg" }, "export");
            if (file != null)
            {

                var exporter = new SvgImageExporter();
                ;
                using var stream = exporter.Export(nodes, settings.Scale);
                await file.SaveAsync(stream);
            }
            else
            {
                throw new OperationCanceledException("Selection file canceled");
            }
        }

        public override void Reset()
        {
            //var pm = ServiceLocator.Current.GetInstance<ProjectManager>();
            //FileName = pm.GetDefaultFileName();
            //base.Reset();
        }

        public override void OnSelected()
        {
            if (string.IsNullOrEmpty(FileName))
            {
                Reset();
            }
            base.OnSelected();
        }
    }
}