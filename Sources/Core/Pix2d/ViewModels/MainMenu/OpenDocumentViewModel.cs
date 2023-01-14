using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Pix2d.Abstract;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.MainMenu
{
    public class OpenDocumentViewModel : MenuItemDetailsViewModelBase
    {
        public IFileService FileService { get; }
        public IProjectService ProjectService { get; }
        public IMenuController MenuController { get; }
        public ICommand OpenCommand => GetCommand(OnOpenCommandExecute);
        public ICommand ImportImageCommand => MapCommand(Commands.Edit.Import, CloseMenu);

        public IRelayCommand OpenRecentProjectCommand => GetCommand<RecentProjectItemViewModel>(async recentProjectVm =>
        {
            CloseMenu();
            var file = recentProjectVm.File;
            await ProjectService.OpenFilesAsync(new[] { file });
        });

        public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; set; } = new ObservableCollection<RecentProjectItemViewModel>();

        public OpenDocumentViewModel(IFileService fileService, IProjectService projectService, IMenuController menuController)
        {
            FileService = fileService;
            ProjectService = projectService;
            MenuController = menuController;

            FileService.MruChanged += FileService_MruChanged;
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

        private void FileService_MruChanged(object sender, EventArgs e)
        {
            UpdateMruData();
        }

        protected void CloseMenu()
        {
            MenuController.ShowMenu = false;
        }

    }
}