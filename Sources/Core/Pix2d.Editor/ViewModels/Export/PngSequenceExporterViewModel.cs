using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.State;
using Pix2d.Exporters;
using Pix2d.State;
using SkiaNodes;

namespace Pix2d.ViewModels.Export
{
    public class PngSequenceExporterViewModel : ExporterViewModel
    {
        public IAppState AppState { get; }
        public override bool ShowFileName { get; set; } = true;
        public string FileNamePrefix { get; set; } = "frame_";
        public PngSequenceExporterViewModel(IDialogService dialogService, IAppState appState) : base(dialogService)
        {
            AppState = appState;
            Name = "Frames to PNG sequence";
            var title = appState.CurrentProject.Title;
            if (!string.IsNullOrWhiteSpace(title))
                FileNamePrefix = title.Replace(" ", "_") + "_";
        }
        public override async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            var fs = ServiceLocator.Current.GetInstance<IFileService>();
            var folder = await fs.GetFolderToExportWithDialogAsync("export");
            if (folder != null)
            {
                //await folder.ClearFolderAsync();
                var files = await folder.GetFilesAsync();
                if (files.Any())
                {
                    var result = await DialogService.ShowYesNoDialog(
                        "Folder is not Empty, files with the same names will be overwritten.",
                        "Folder is not Empty");

                    if(!result)
                        return;
                }
                var exporter = new SpritePngSequenceExporter();
                exporter.FileNamePrefix = FileNamePrefix;
                exporter.Export(folder, nodes, settings.Scale);
            }
            else
            {
                throw new OperationCanceledException("Selection directory canceled");
            }

        }

        public override Task<Stream> ExportToStream(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            throw new NotImplementedException();
        }

        public override void Reset()
        {
            //var pm = ServiceLocator.Current.GetInstance<ProjectManager>();
            //FileName = pm.GetDefaultFileName();
            base.Reset();
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