using Pix2d.Common.Extensions;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.MainMenu;

public class KeyShortcutsView : LocalizedComponentBase
{
    private static readonly IImmutableBrush HeaderBrush = Colors.White.WithAlpha(0.6f).ToBrush().ToImmutable();
    private static readonly IImmutableBrush ShortcutBrush = Colors.White.WithAlpha(0.9f).ToBrush().ToImmutable();
    [Inject] ICommandService CommandService { get; set; } = null!;

    protected override object Build()
    {
        var commands = CommandService.GetCommands().Where(c => c.DefaultShortcut != null).ToList();
        return new ItemsControl()
            .ItemsSource(commands)
            .ItemTemplate((Pix2dCommand item) =>
                new FuncComponent<Pix2dCommand>(item, _ =>
                    new Grid().Cols("*,*")
                        .Margin(bottom: 6)
                        .Children(
                            new TextBlock()
                                .Text(L(item.Description))
                                .Foreground(HeaderBrush)
                                .FontSize(16)
                                .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                                .TextWrapping(TextWrapping.Wrap),
                            new TextBlock().Text(item.GetShortcutString()).Col(1)
                                .FontSize(16)
                                .Margin(left: 10)
                                .Foreground(ShortcutBrush)
                        ))
            );
    }
}