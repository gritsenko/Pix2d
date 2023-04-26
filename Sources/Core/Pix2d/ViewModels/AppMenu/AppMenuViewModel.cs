using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.AppMenu;

public class AppMenuViewModel : Pix2dViewModelBase
{
    public ICommandService CommandService { get; }
    public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new ObservableCollection<AppMenuItemViewModel>();

    public AppMenuViewModel(ICommandService commandService)
    {
        CommandService = commandService;
    }

    protected override void OnLoad()
    {
        var commands = CommandService.GetCommands();

        var items = new Dictionary<string, AppMenuItemViewModel>();

        MenuItems.Clear();

        AppMenuItemViewModel AddItem(string name)
        {
            var item = new AppMenuItemViewModel(name);
            items[name] = item;
            MenuItems.Add(item);
            return item;
        }

        AddItem("File");
        AddItem("Edit");
        AddItem("View");
        AddItem("Project");
        AddItem("Tools");
        AddItem("Window");

        foreach (var pix2dCommand in commands)
        {
            var topGroup = pix2dCommand.Groups[0];

            if (!items.TryGetValue(topGroup, out var item)) item = AddItem(topGroup);

            if (pix2dCommand.Groups.Length < 2)
                continue;

            var lastGroup = pix2dCommand.Groups.LastOrDefault();

            var commandItem = new AppMenuItemViewModel(lastGroup, async () => await CommandService.ExecuteCommandAsync(pix2dCommand.Name), pix2dCommand.GetShortcutString());

            item.MenuItems.Add(commandItem);
        }
    }
}