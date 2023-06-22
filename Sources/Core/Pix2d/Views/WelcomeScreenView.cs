using Pix2d.Shared;

namespace Pix2d.Views;

public class WelcomeScreenView : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Children(
                new AppButton()
                    .Classes("TopBar")
                    .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                    .Label("Clear")
                    .Width(68)
                    .Content("\xE894")
            );
}