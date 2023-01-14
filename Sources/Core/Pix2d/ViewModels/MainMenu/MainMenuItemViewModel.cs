using System;
using Mvvm;

namespace Pix2d.ViewModels.MainMenu
{
    public class MainMenuItemViewModel : ViewModelBase
    {
        public Type DetailsViewModel { get; }
        public string Name { get; set; }
        public char IconGlyph { get; set; }

        public IRelayCommand SelectCommand { get; set; }

        public Action OnSelectAction { get; set; }

        public bool IsSelected
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool IsSplitter { get; set; }

        public MainMenuItemViewModel(string name, Type detailsVm = null)
        {
            DetailsViewModel = detailsVm;
            //DetailsViewModel.Name = name;
            Name = name;
        }
    }
}