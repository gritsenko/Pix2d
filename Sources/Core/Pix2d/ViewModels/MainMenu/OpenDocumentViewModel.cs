using System.Collections.ObjectModel;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract.Platform;
using Pix2d.Messages;

namespace Pix2d.ViewModels.MainMenu;

public class OpenDocumentViewModel : MenuItemDetailsViewModelBase
{
    public IFileService FileService { get; }
    public IProjectService ProjectService { get; }
    public ICommand OpenCommand => GetCommand(OnOpenCommandExecute);

    public IRelayCommand OpenRecentProjectCommand => GetCommand<RecentProjectItemViewModel>(async recentProjectVm =>
    {
        CloseMenu();
        var file = recentProjectVm.File;
        await ProjectService.OpenFilesAsync(new[] { file });
    });

    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; set; } = new();

    public OpenDocumentViewModel(IFileService fileService, IProjectService projectService, IMessenger messenger)
    {
        FileService = fileService;
        ProjectService = projectService;

        messenger.Register<MruChangedMessage>(this, m => UpdateMruData());
    }

    protected override void OnLoad()
    {
        UpdateMruData(); // update recent files list on any open, file can be deleted by user when program is run
    }

    public async void UpdateMruData()
    {
        var projectService = CoreServices.ProjectService;
        var recentProjects = await projectService.GetRecentProjectsAsync();

        RecentProjects.Clear();
        foreach (var fileContentSource in recentProjects)
        {
            RecentProjects.Add(new RecentProjectItemViewModel(fileContentSource)
            {
                OpenRecentProjectCommand = this.OpenRecentProjectCommand
            });
        }
    }
    private async void OnOpenCommandExecute()
    {
        CloseMenu();
        await ProjectService.OpenFilesAsync();
    }

    protected void CloseMenu()
    {
        Commands.View.HideMainMenuCommand.Execute();
    }

}