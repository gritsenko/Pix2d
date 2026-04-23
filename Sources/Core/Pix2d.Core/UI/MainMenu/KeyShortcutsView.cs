using Avalonia.Styling; // For styles
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Common.Extensions;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using System.Collections.ObjectModel;

namespace Pix2d.UI.MainMenu;

public partial class KeyShortcutsView(ICommandService commandService) : ViewBase<KeyShortcutsView.State>(new State(commandService))
{
    // Brushes
    private static readonly IImmutableBrush HeaderBrush = Colors.White.WithAlpha(0.6f).ToBrush().ToImmutable();
    private static readonly IImmutableBrush ShortcutBrush = Colors.White.WithAlpha(0.9f).ToBrush().ToImmutable();
    private static readonly IImmutableBrush GroupHeaderBrush = Colors.White.WithAlpha(0.9f).ToBrush().ToImmutable();

    // Background for "zebra" (barely noticeable)
    private static readonly IImmutableBrush OddRowBrush = Colors.White.WithAlpha(0.03f).ToBrush().ToImmutable();
    // Background on hover
    private static readonly IImmutableBrush HoverBrush = Colors.White.WithAlpha(0.08f).ToBrush().ToImmutable();

    private const double MinColumnWidth = 300;

    protected override StyleGroup? BuildStyles() =>
    [
        new Style<Border>(x => x.Class("ShortcutRow"))
            .CornerRadius(4)
            .Background(Brushes.Transparent),

        new Style<Border>(x => x.Class("ShortcutRow").Class("Odd"))
            .Background(OddRowBrush),

        new Style<Border>(x => x.Class("ShortcutRow").Class(":pointerover"))
            .Background(HoverBrush)
    ];

    protected override object Build(State state)
    {
        return new Grid()
            .OnSizeChanged(e => state.RecalculateColumns(e.NewSize.Width))
            .Children([
                new ItemsControl()
                    .ItemsSource(state.ColumnsData)
                    .ItemsPanel(new FuncTemplate<Panel?>(() => new UniformGrid().Rows(1)))
                    .ItemTemplate((List<IGrouping<string, Pix2dCommand>> columnGroups) =>
                        new ItemsControl()
                            .Margin(12)
                            .ItemsSource(columnGroups)
                            .ItemTemplate((IGrouping<string, Pix2dCommand> group) => RenderGroup(group))
                    )
            ]);
    }

    private Control RenderGroup(IGrouping<string, Pix2dCommand> group)
    {
        var groupAccentColor = GetGroupColor(group.Key);

        var itemsWithIndex = group.Select((command, index) => new ShortcutRowItem(command, index)).ToList();

        return new StackPanel()
            .Margin(0, 0, 0, 24)
            .Background(Colors.Black.WithAlpha(0.2f).ToBrush())
            .Children([
                new Border()
                    .Padding(10, 5, 0, 5)
                    .BorderThickness(left: 3, top: 0, right: 0, bottom: 0)
                    .BorderBrush(groupAccentColor)
                    .Child(
                        new TextBlock()
                            .Text(L(group.Key))
                            .Foreground(GroupHeaderBrush)
                            .FontSize(18)
                            .FontWeight(FontWeight.SemiBold)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                    ),

                new ItemsControl()
                    .Margin(0, 8, 0, 0)
                    .ItemsSource(itemsWithIndex)
                    .ItemTemplate((ShortcutRowItem item) => CreateShortcutRow(item.Command, item.Index))
            ]);
    }

    private Control CreateShortcutRow(Pix2dCommand command, int index)
    {
        return new Border()
            .Classes("ShortcutRow")
            .Classes(index % 2 != 0 ? "Odd" : "")
            .Padding(8, 6)
            .Child(
                new Grid().Cols("*,Auto")
                    .Classes("ShortcutRowGrid")
                    .Children([
                        new TextBlock()
                            .Text(L(command.Description))
                            .Foreground(HeaderBrush)
                            .FontSize(15)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                            .TextWrapping(TextWrapping.Wrap)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(0, 0, 12, 0),

                        new Border()
                            .Col(1)
                            .CornerRadius(4)
                            .Background(Colors.White.WithAlpha(0.1f).ToBrush())
                            .Padding(6, 2)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Child(
                                new TextBlock()
                                    .Text(command.GetShortcutString())
                                    .FontSize(14)
                                    .Foreground(ShortcutBrush)
                                    .FontWeight(FontWeight.Bold)
                                    .FontFamily("Consolas, Monospace")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                            )
                    ])
            );
    }

    // Helper for generating a deterministic color based on a string
    private IBrush GetGroupColor(string key)
    {
        int hash = Math.Abs(key.GetHashCode());
        var colors = new[]
        {
            Colors.CadetBlue, Colors.IndianRed, Colors.MediumSeaGreen,
            Colors.Goldenrod, Colors.MediumPurple, Colors.SlateBlue,
            Colors.Tomato, Colors.SteelBlue
        };

        var color = colors[hash % colors.Length];
        return color.ToBrush().ToImmutable();
    }

    private sealed record ShortcutRowItem(Pix2dCommand Command, int Index);

    public sealed partial class State : ObservableObject
    {
        private readonly List<IGrouping<string, Pix2dCommand>> _allGroups;
        private int _currentColumnCount;

        [ObservableProperty]
        public partial ObservableCollection<List<IGrouping<string, Pix2dCommand>>> ColumnsData { get; set; } = [];

        public State(ICommandService commandService)
        {
            _allGroups = commandService.GetCommands()
                .Where(command => command.DefaultShortcut != null)
                .GroupBy(command => command.Groups.Length > 0 ? command.Groups[0] : "Other")
                .OrderBy(group => group.Key)
                .ToList();
        }

        public void RecalculateColumns(double containerWidth)
        {
            if (containerWidth <= 0)
                return;

            var desiredColumns = (int)(containerWidth / MinColumnWidth);
            var actualColumns = Math.Clamp(desiredColumns, 1, 4);

            if (actualColumns == _currentColumnCount)
                return;

            _currentColumnCount = actualColumns;

            var tempColumns = new List<List<IGrouping<string, Pix2dCommand>>>();
            for (var index = 0; index < actualColumns; index++)
            {
                tempColumns.Add([]);
            }

            for (var index = 0; index < _allGroups.Count; index++)
            {
                tempColumns[index % actualColumns].Add(_allGroups[index]);
            }

            ColumnsData.Clear();
            foreach (var column in tempColumns)
            {
                ColumnsData.Add(column);
            }
        }
    }
}