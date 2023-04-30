using System.Windows.Input;
using Pix2d.Mvvm;
using Pix2d.ViewModels.Export;

namespace Pix2d.ViewModels.MainMenu;

public class SaveDocumentViewModel : MenuItemDetailsViewModelBase
{
    public IProjectService ProjectService { get; }
    public IViewModelService ViewModelService { get; }

    public ICommand SaveAsCommand => GetCommand(OnSaveAsCommandExecute);
    public ICommand SaveAsPngCommand => GetCommand(() =>
    {
        CloseMenu();
        Commands.View.ShowExportDialogCommand.Execute();

        var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
        exportVm.SelectExporterByFileType(ExportImportProjectType.Png);
    });

    public ICommand SaveAsGifAnimationCommand => GetCommand(() =>
    {
        CloseMenu();
        Commands.View.ShowExportDialogCommand.Execute();

        var exportVm = ViewModelService.GetViewModel<ExportPageViewModel>();
        exportVm.SelectExporterByFileType(ExportImportProjectType.Gif);
    });

    public SaveDocumentViewModel(IProjectService projectService, IViewModelService viewModelService)
    {
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
        Commands.View.HideMainMenuCommand.Execute();
    }

}