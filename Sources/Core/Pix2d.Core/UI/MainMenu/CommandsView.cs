using Pix2d.UI.Common;
using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public class CommandsView : ComponentBase
{
    protected override object Build() =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).Children(
                new TextBlock().Text(L("Keyboard shortcuts:")).Margin(0, 32, 0, 16).FontSize(20)
                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),

                new KeyShortcutsView()
                    .IsVisible(PlatformStuffService.HasKeyboard)
            ));

    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;
}
