using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels
{
    public class TabsViewModel : Pix2dViewModelBase
    {
        public ObservableCollection<TabItemViewModel> Tabs { get; set; } = new();

        protected override void OnLoad()
        {
            Tabs.Clear();
            Tabs.Add(new TabItemViewModel() {Title = "Doc1"});
            Tabs.Add(new TabItemViewModel() {Title = "Doc2"});
        }
    }

    public class TabItemViewModel
    {
        public string Title { get; set; }
    }
}