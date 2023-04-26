using System.Windows.Input;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;
using Pix2d.ViewModels.Export;

namespace Pix2d.ViewModels.MainMenu
{
    public class SaveDocumentViewModel : MenuItemDetailsViewModelBase
    {
        public IMenuController MenuController { get; }
        public IProjectService ProjectService { get; }
        public IViewModelService ViewModelService { get; }

        public ICommand SaveAsCommand => GetCommand(OnSaveAsCommandExecute);
        public ICommand SaveAsPngCommand => GetCommand(() =>
        {
            CloseMenu();
            MenuController.ShowExportDialog = true;

            var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
            exportVm.SelectExporterByFileType(ExportImportProjectType.Png);
        });

        public ICommand SaveAsGifAnimationCommand => GetCommand(() =>
        {
            CloseMenu();
            MenuController.ShowExportDialog = true;

            var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
            exportVm.SelectExporterByFileType(ExportImportProjectType.Gif);
        });

        public SaveDocumentViewModel(IMenuController menuController, IProjectService projectService, IViewModelService viewModelService)
        {
            MenuController = menuController;
            ProjectService = projectService;
            ViewModelService = viewModelService;
        }

        private async void OnSaveAsCommandExecute()
        {
            CloseMenu();
            await ProjectService.SaveCurrentProjectAsAsync(ExportImportProjectType.Pix2d);
        }

        protected void CloseMenu()
        {
            MenuController.ShowMenu = false;
        }

    }
}