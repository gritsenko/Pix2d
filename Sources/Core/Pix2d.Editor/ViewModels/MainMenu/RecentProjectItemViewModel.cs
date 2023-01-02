using System.Windows.Input;
using Pix2d.Abstract.Platform.FileSystem;

namespace Pix2d.ViewModels.MainMenu
{
    public class RecentProjectItemViewModel
    {
        public ICommand OpenRecentProjectCommand { get; set; }

        public RecentProjectItemViewModel(IFileContentSource fileContentSource)
        {
            File = fileContentSource;
        }

        public IFileContentSource File { get; set; }

        public string Path => File.Path;
        public string Title => File.Title;
    }
}