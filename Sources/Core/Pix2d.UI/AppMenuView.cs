using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Styling;
using Mvvm;
using Pix2d.Primitives;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public class AppMenuView : ComponentBase
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
            .ItemsSource(Bind(MenuItems))
            .Styles(AppMenuStyle);


    [Inject] private ICommandService CommandService { get; set; } = null!;
    public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new();

    protected override void OnAfterInitialized()
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

            var commandItem = new AppMenuItemViewModel(lastGroup, pix2dCommand);

            item.MenuItems.Add(commandItem);
        }
    }

    public class AppMenuItemViewModel : ObservableObject
    {

        public AppMenuItemViewModel(string header, Pix2dCommand? command = null)
        {
            Header = header;

            if (command == null) return;
            
            Shortcut = command.GetShortcutString();
            Command = command;
        }

        public string Header { get; set; }
        public string? Shortcut { get; set; }
        public ICommand? Command { get; }

        public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = new();
    }
}