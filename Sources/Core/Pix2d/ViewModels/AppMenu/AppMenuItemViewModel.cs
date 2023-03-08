using System;
using System.Collections.ObjectModel;
using Mvvm;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.AppMenu;

public class AppMenuItemViewModel : Pix2dViewModelBase
{

    public AppMenuItemViewModel(string header, Action action = default, string shortcut = default)
    {
        Header = header;
        Shortcut = shortcut;
        _action = action;
    }

    public string Header { get; set; }
    public string Shortcut { get; set; }

    private Action _action;

    public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new();

    public IRelayCommand Command => _action != null ? GetCommand(_action) : null;

}