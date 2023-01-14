using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using CommonServiceLocator;
using Mvvm;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.UI;
using SkiaNodes;

namespace Pix2d.ViewModels.Export
{
    public abstract class ExporterViewModel : ViewModelBase
    {
        public IDialogService DialogService { get; }
        public string Name { get; set; }
        public virtual bool ShowFileName { get; set; }
        public virtual bool ShowSpritesheetOptions { get; set; }

        public string FileName { get; set; }
        public ExporterViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
        }

        public virtual async Task Export(IEnumerable<SKNode> nodes, ImageExportSettings settings)
        {
            try
            {
                var vm = ServiceLocator.Current.GetInstance<IMenuController>();
                vm.ShowExportDialog = false;

                //var pm = ServiceLocator.Current.GetInstance<ProjectManager>();

            }
            catch (Exception e)
            {
                //Logger.LogEventWithParams("Exported image", new Dictionary<string, string>()
                //{
                //    {"with", pm.ArtWorkPresenter.CurrentArtwork.Size.Width.ToString()},
                //    {"height", pm.ArtWorkPresenter.CurrentArtwork.Size.Height.ToString()},
                //});
                Logger.LogException(e);
            }
        }

        public abstract Task<Stream> ExportToStream(IEnumerable<SKNode> node, ImageExportSettings settings);

        public virtual void OnSelected()
        {
            
        }

        public virtual void Reset()
        {
            
        }

    }
}