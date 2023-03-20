using Pix2d.Shared;

namespace Pix2d.Views;

public class WelcomeScreenView : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Children(
                new AppButton()
                    .With(ButtonStyle)
                    .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                    .Label("Clear")
                    .Content("\xE894")
            );

    private void ButtonStyle(AppButton b) => b
            .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
            .Width(68)
            .Classes("TopBar");
}