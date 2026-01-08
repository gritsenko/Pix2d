using Pix2d.Common.Extensions;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using System.Collections.ObjectModel;
using Avalonia.Styling; // Для стилей

namespace Pix2d.UI.MainMenu;

public class KeyShortcutsView : LocalizedComponentBase
{
    // Кисти
    private static readonly IImmutableBrush HeaderBrush = Colors.White.WithAlpha(0.6f).ToBrush().ToImmutable();
    private static readonly IImmutableBrush ShortcutBrush = Colors.White.WithAlpha(0.9f).ToBrush().ToImmutable();
    private static readonly IImmutableBrush GroupHeaderBrush = Colors.White.WithAlpha(0.9f).ToBrush().ToImmutable();

    // Фон для "зебры" (еле заметный)
    private static readonly IImmutableBrush OddRowBrush = Colors.White.WithAlpha(0.03f).ToBrush().ToImmutable();
    // Фон при наведении
    private static readonly IImmutableBrush HoverBrush = Colors.White.WithAlpha(0.08f).ToBrush().ToImmutable();

    private const double MinColumnWidth = 300;

    [Inject] ICommandService CommandService { get; set; } = null!;

    private readonly ObservableCollection<List<IGrouping<string, Pix2dCommand>>> _columnsData = new();
    private int _currentColumnCount = 0;


    protected override StyleGroup? BuildStyles() =>
    [
        new Style<Border>(x => x.Class("ShortcutRow"))
            .CornerRadius(4),

        new Style<Grid>(x => x.Class("ShortcutRowGrid"))
            .Background(Brushes.Transparent),

        new Style<Grid>(x => x.Class("ShortcutRowGrid").Class(":pointerover"))
            .Background(HoverBrush)
    ];

    protected override object Build()
    {
        var commands = CommandService.GetCommands().Where(c => c.DefaultShortcut != null).ToList();

        var allGroups = commands
            .GroupBy(c => c.Groups.Length > 0 ? c.Groups[0] : "Other")
            .OrderBy(g => g.Key)
            .ToList();
       
        // --- Логика пересчета колонок (осталась прежней) ---
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
                    .ItemsPanel(new FuncTemplate<Panel?>(() => new UniformGrid().Rows(1)))
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
        // Генерируем уникальный цвет для группы на основе её имени
        var groupAccentColor = GetGroupColor(group.Key);

        // Преобразуем данные, добавляя индекс для Зебры
        var itemsWithIndex = group.Select((cmd, index) => new { Command = cmd, Index = index }).ToList();

        return new FuncComponent<IGrouping<string, Pix2dCommand>>(group, _ =>
            new StackPanel()
                .Margin(bottom: 24)
                // Легкая подложка под всю группу (опционально, можно убрать Background)
                .Background(Colors.Black.WithAlpha(0.2f).ToBrush())
                .Children([
                    
                    // --- ЗАГОЛОВОК ГРУППЫ ---
                    new Border()
                        .Padding(left: 10, top: 5, bottom: 5)
                        // Цветная полоска слева (Accent Color)
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
                    
                    // --- СПИСОК ЭЛЕМЕНТОВ ---
                    new ItemsControl()
                        .Margin(top: 8)
                        .ItemsSource(itemsWithIndex) // Используем список с индексами
                        .ItemTemplate((dynamic itemCtx) =>
                        {
                            Pix2dCommand item = itemCtx.Command;
                            int index = itemCtx.Index;
                            
                            // Определяем цвет "зебры"
                            var rowBackground = index % 2 == 0 ? OddRowBrush : Brushes.Transparent;

                            // Используем FuncComponent для производительности
                            return new FuncComponent<Pix2dCommand>(item, _ =>
                                new Border()
                                    .Classes("ShortcutRow") // Применяем CSS класс для Ховера
                                    .Background(rowBackground)
                                    .Padding(8, 6) // Внутренний отступ строки
                                    .Child(
                                        new Grid().Cols("*,Auto")
                                            .Classes("ShortcutRowGrid")
                                            .Children([
                                                // Описание
                                                new TextBlock()
                                                    .Text(L(item.Description))
                                                    .Foreground(HeaderBrush)
                                                    .FontSize(15)
                                                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                                                    .TextWrapping(TextWrapping.Wrap)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .Margin(right: 12),
                                                    
                                                // Шорткат (Кнопка-вид)
                                                new Border()
                                                    .Col(1)
                                                    .CornerRadius(4)
                                                    .Background(Colors.White.WithAlpha(0.1f).ToBrush()) // Подложка под клавиши
                                                    .Padding(6, 2)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .Child(
                                                        new TextBlock()
                                                            .Text(item.GetShortcutString())
                                                            .FontSize(14)
                                                            .Foreground(ShortcutBrush)
                                                            .FontWeight(FontWeight.Bold)
                                                            .FontFamily("Consolas, Monospace") // Моноширинный для клавиш
                                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                                    )
                                            ])
                                    )
                            );
                        })
                ])
        );
    }

    // Хелпер для генерации детерминированного цвета по строке
    private IBrush GetGroupColor(string key)
    {
        // Простой хеш для выбора цвета
        int hash = Math.Abs(key.GetHashCode());

        // Палитра приятных цветов (можно расширить)
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