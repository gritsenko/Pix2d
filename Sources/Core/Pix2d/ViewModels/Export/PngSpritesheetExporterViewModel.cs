using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Platform;
using Pix2d.Exporters;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.ViewModels.Export
{
    public class PngSpritesheetExporterViewModel : ExporterViewModel
    {
        public override bool ShowFileName { get; set; } = false;

        public override bool ShowSpritesheetOptions { get; set; } = true;

        public event EventHandler ColumnsChanged;

        public int Columns
        {
            get => Get<int>();
            set
            {
                if (Set(value))
                {
                    OnColumnsChanged();
                }
            }
        }
        public PngSpritesheetExporterViewModel(IDialogService dialogService) : base(dialogService)
        {
            Name = "Spritesheet (PNG)";
        }

        public override async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            var fs = ServiceLocator.Current.GetInstance<IFileService>();
            var firstNode = nodes.FirstOrDefault();
            var file = await fs.GetFileToSaveWithDialogAsync(settings.DefaultFileName ?? "Sprite.png", new[] { ".png" }, "export");
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
            var exporter = new SpritesheetImageExporter() {MaxColumns = Columns};
            return Task.FromResult(exporter.Export(nodes, settings.Scale));
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
        protected virtual void OnColumnsChanged()
        {
            ColumnsChanged?.Invoke(this, EventArgs.Empty);
        }

    }
}