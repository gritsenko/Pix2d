using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Styling;
using Mvvm;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

public class AppMenuView : LocalizedComponentBase
{
    protected Style AppMenuStyle => new(s => s.OfType<MenuItem>())
    {
        Setters =
        {
            new Setter(MenuItem.HeaderProperty, new Binding("Header")),
            new Setter(MenuItem.ItemsSourceProperty, new Binding("MenuItems")),
            new Setter(MenuItem.CommandProperty, new Binding("Command"))
        }
    };

    protected override object Build() =>
        new Menu()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Foreground(Colors.White.ToBrush())
            .Padding(4)
            .ItemsSource(() => MenuItems)
            .Styles(AppMenuStyle);


    [Inject] private ICommandService CommandService { get; set; } = null!;
    public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new();

    protected override void OnAfterInitialized()
    {
        RebuildMenu();
    }

    private void RebuildMenu()
    {
        var commands = CommandService.GetCommands();
        var items = new Dictionary<string, AppMenuItemViewModel>();

        MenuItems.Clear();

        AppMenuItemViewModel AddItem(string groupName)
        {
            var title = L(groupName)();
            var item = new AppMenuItemViewModel(groupName, title);
            items[groupName] = item;
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

            if (!items.TryGetValue(topGroup, out var item)) 
                item = AddItem(topGroup);

            if (pix2dCommand.Groups.Length < 2)
                continue;

            var lastGroup = pix2dCommand.Groups.LastOrDefault();
            var title = L(lastGroup)();
            var commandItem = new AppMenuItemViewModel(lastGroup, title, pix2dCommand);

            item.MenuItems.Add(commandItem);
        }

    }

    public class AppMenuItemViewModel : ObservableObject
    {

        public AppMenuItemViewModel(string groupName, string header, Pix2dCommand? command = null)
        {
            GroupName = groupName;
            Header = header;

            if (command == null) return;

            Shortcut = command.GetShortcutString();
            Command = command;
        }

        public string GroupName { get; }
        public string Header { get; }
        public string? Shortcut { get; }
        public ICommand? Command { get; }

        public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new();
    }
}