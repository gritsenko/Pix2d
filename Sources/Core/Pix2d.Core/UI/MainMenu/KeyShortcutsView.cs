using Avalonia.Styling; // For styles
using Pix2d.Common.Extensions;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using System.Collections.ObjectModel;

namespace Pix2d.UI.MainMenu;

public class KeyShortcutsView : LocalizedComponentBase
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

    [Inject] ICommandService CommandService { get; set; } = null!;

    private readonly ObservableCollection<List<IGrouping<string, Pix2dCommand>>> _columnsData = new();
    private int _currentColumnCount = 0;


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

    protected override object Build()
    {
        var commands = CommandService.GetCommands().Where(c => c.DefaultShortcut != null).ToList();

        var allGroups = commands
            .GroupBy(c => c.Groups.Length > 0 ? c.Groups[0] : "Other")
            .OrderBy(g => g.Key)
            .ToList();

        // --- Logic for recalculating columns (remains the same) ---
        void RecalculateColumns(double containerWidth)
        {
            if (containerWidth <= 0) return;
            int desiredCols = (int)(containerWidth / MinColumnWidth);
            int actualCols = Math.Clamp(desiredCols, 1, 4);

            if (actualCols == _currentColumnCount) return;
            _currentColumnCount = actualCols;

            var tempColumns = new List<List<IGrouping<string, Pix2dCommand>>>();
            for (int i = 0; i < actualCols; i++) tempColumns.Add(new List<IGrouping<string, Pix2dCommand>>());

            for (int i = 0; i < allGroups.Count; i++)
            {
                tempColumns[i % actualCols].Add(allGroups[i]);
            }

            _columnsData.Clear();
            foreach (var col in tempColumns) _columnsData.Add(col);
        }

        return new Grid()
            .OnSizeChanged(e => RecalculateColumns(e.NewSize.Width))
            .Children([
                new ItemsControl()
                    .ItemsSource(_columnsData)
                    .ItemsPanel(new FuncTemplate<Panel>(() => new UniformGrid().Rows(1)))
                    .ItemTemplate((List<IGrouping<string, Pix2dCommand>> columnGroups) =>
                        new ItemsControl()
                            .Margin(12)
                            .ItemsSource(columnGroups)
                            .ItemTemplate((IGrouping<string, Pix2dCommand> group) => RenderGroup(group))
                    )
            ]);
    }

    private FuncComponent<IGrouping<string, Pix2dCommand>> RenderGroup(IGrouping<string, Pix2dCommand> group)
    {
        // Generate a unique color for the group based on its name
        var groupAccentColor = GetGroupColor(group.Key);

        // Transform data, adding an index for the zebra effect
        var itemsWithIndex = group.Select((cmd, index) => new { Command = cmd, Index = index }).ToList();

        return new FuncComponent<IGrouping<string, Pix2dCommand>>(group, _ =>
            new StackPanel()
                .Margin(bottom: 24)
                // Light background under the entire group (optional, can remove Background)
                .Background(Colors.Black.WithAlpha(0.2f).ToBrush())
                .Children([
                    
                    // --- GROUP HEADER ---
                    new Border()
                        .Padding(left: 10, top: 5, bottom: 5)
                        // Colored stripe on the left (Accent Color)
                        .BorderThickness(left: 3, top:0, right:0, bottom:0)
                        .BorderBrush(groupAccentColor)
                        .Child(
                            new TextBlock()
                                .Text(L(group.Key))
                                .Foreground(GroupHeaderBrush)
                                .FontSize(18)
                                .FontWeight(FontWeight.SemiBold)
                                .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                        ),
                    
                    // --- LIST OF ITEMS ---
                    new ItemsControl()
                        .Margin(top: 8)
                        .ItemsSource(itemsWithIndex) // Use the list with indices
                        .ItemTemplate((dynamic itemCtx) =>
                        {
                            Pix2dCommand item = itemCtx.Command;
                            int index = itemCtx.Index;
                            // Use FuncComponent for performance
                            return new FuncComponent<Pix2dCommand>(item, _ =>
                                new Border()
                                    .Classes("ShortcutRow") // Apply CSS class for Hover
                                    .Classes(index % 2 != 0 ? "Odd" : "")
                                    .Padding(8, 6) // Inner padding of the row
                                    .Child(
                                        new Grid().Cols("*,Auto")
                                            .Classes("ShortcutRowGrid")
                                            .Children([
                                                // Description
                                                new TextBlock()
                                                    .Text(L(item.Description))
                                                    .Foreground(HeaderBrush)
                                                    .FontSize(15)
                                                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                                                    .TextWrapping(TextWrapping.Wrap)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .Margin(right: 12),
                                                
                                                // Shortcut (Button-like view)
                                                new Border()
                                                    .Col(1)
                                                    .CornerRadius(4)
                                                    .Background(Colors.White.WithAlpha(0.1f).ToBrush()) // Background under keys
                                                    .Padding(6, 2)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .Child(
                                                        new TextBlock()
                                                            .Text(item.GetShortcutString())
                                                            .FontSize(14)
                                                            .Foreground(ShortcutBrush)
                                                            .FontWeight(FontWeight.Bold)
                                                            .FontFamily("Consolas, Monospace") // Monospaced for keys
                                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                                    )
                                            ])
                                    )
                            );
                        })
                ])
        );
    }

    // Helper for generating a deterministic color based on a string
    private IBrush GetGroupColor(string key)
    {
        // Simple hash for selecting a color
        int hash = Math.Abs(key.GetHashCode());

        // Palette of pleasant colors (can be expanded)
        var colors = new[]
        {
            Colors.CadetBlue, Colors.IndianRed, Colors.MediumSeaGreen,
            Colors.Goldenrod, Colors.MediumPurple, Colors.SlateBlue,
            Colors.Tomato, Colors.SteelBlue
        };

        var color = colors[hash % colors.Length];
        return color.ToBrush().ToImmutable();
    }
}