using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Mvvm;
using Pix2d.Abstract;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels;

public class AppMenuItemVm : Pix2dViewModelBase
{

    public AppMenuItemVm(string header, Action action = default, string shortcut = default)
    {
        Header = header;
        Shortcut = shortcut;
        _action = action;
    }

    public string Header { get; set; }
    public string Shortcut { get; set; }

    private Action _action;

    public ObservableCollection<AppMenuItemVm> MenuItems { get; } = new ObservableCollection<AppMenuItemVm>();

    public IRelayCommand Command => _action != null ? GetCommand(_action) : null;

}

public class AppMenuViewModel : Pix2dViewModelBase
{
    public ICommandService CommandService { get; }
    public ObservableCollection<AppMenuItemVm> MenuItems { get; } = new ObservableCollection<AppMenuItemVm>();

    public AppMenuViewModel(ICommandService commandService)
    {
        CommandService = commandService;
    }

    protected override void OnLoad()
    {
        var commands = CommandService.GetCommands();

        var items = new Dictionary<string, AppMenuItemVm>();

        MenuItems.Clear();

        AppMenuItemVm AddItem(string name)
        {
            var item = new AppMenuItemVm(name);
            items[name] = item;
            MenuItems.Add(item);
            return item;
        }

        AddItem("File");
        AddItem("Edit");
        AddItem("View");
        AddItem("Project");
        AddItem("Tools");

        foreach (var pix2dCommand in commands)
        {
            var topGroup = pix2dCommand.Groups[0];

            if (!items.TryGetValue(topGroup, out var item)) item = AddItem(topGroup);

            if (pix2dCommand.Groups.Length < 2)
                continue;

            var lastGroup = pix2dCommand.Groups.LastOrDefault();

            var commandItem = new AppMenuItemVm(lastGroup, async () => await CommandService.ExecuteCommandAsync(pix2dCommand.Name), pix2dCommand.GetShortcutString());

            item.MenuItems.Add(commandItem);
        }
    }
}