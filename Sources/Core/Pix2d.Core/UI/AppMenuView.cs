using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Primitives;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public partial class AppMenuView(ICommandService commandService) : ViewBase<AppMenuView.State>(new State(commandService))
{
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<MenuItem>()
        {
            Setters =
            {
                new Setter(MenuItem.ItemsSourceProperty, new Binding(nameof(AppMenuItemViewModel.MenuItems))),
                new Setter(MenuItem.CommandProperty, new Binding(nameof(AppMenuItemViewModel.Command))),
                new Setter(MenuItem.HeaderTemplateProperty, new FuncDataTemplate<AppMenuItemViewModel>((item, _) => BuildMenuHeader(item)))
            }
        },
    ];

    protected override object Build(State state) =>
        new Menu()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Foreground(Brushes.White)
            .Padding(4)
            .ItemsSource(state.MenuItems);

    private static Control BuildMenuHeader(AppMenuItemViewModel? item) =>
        new Grid()
            .Cols("*, Auto")
            .Children(
                new TextBlock()
                    .Text(item?.Header ?? string.Empty)
                    .FontSize(12)
                    .FontWeight(FontWeight.Regular)
                    .Foreground(Brushes.White)
                    .Padding(2, 0, 2, 0),
                new TextBlock()
                    .Col(1)
                    .IsVisible(!string.IsNullOrWhiteSpace(item?.Shortcut))
                    .Text(item?.Shortcut ?? string.Empty)
                    .TextAlignment(TextAlignment.Right)
                    .FontSize(14)
                    .Foreground(Brushes.LightGray)
                    .MinWidth(100)
                    .Padding(2, 0, 0, 0)
            );

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<AppMenuItemViewModel> MenuItems { get; set; } = [];

        public State(ICommandService commandService)
        {
            RebuildMenu(commandService);
        }

        private void RebuildMenu(ICommandService commandService)
        {
            var commands = commandService.GetCommands();
            var items = new Dictionary<string, AppMenuItemViewModel>();

            MenuItems.Clear();

            AppMenuItemViewModel AddItem(string groupName)
            {
                var title = L(groupName);
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

                var lastGroup = pix2dCommand.Groups.Last();
                var title = L(lastGroup);
                item.MenuItems.Add(new AppMenuItemViewModel(lastGroup, title, pix2dCommand));
            }
        }
    }

    public sealed class AppMenuItemViewModel
    {
        public AppMenuItemViewModel(string groupName, string header, Pix2dCommand? command = null)
        {
            GroupName = groupName;
            Header = header;

            if (command == null)
                return;

            Shortcut = command.GetShortcutString();
            Command = command;
        }

        public string GroupName { get; }
        public string Header { get; }
        public string? Shortcut { get; }
        public ICommand? Command { get; }
        public ObservableCollection<AppMenuItemViewModel> MenuItems { get; } = [];
    }
}