namespace Pix2d.Views.MainMenu;

public class SupportView : ComponentBase
{
    protected override object Build() =>
        new Grid().Children(
            new Button()
                .VerticalAlignment(VerticalAlignment.Center)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .Content("https://boosty.to/pix2d")
                .OnClick(args =>
                {
                    PlatformStuffService.OpenUrlInBrowser("https://boosty.to/pix2d");
                })
        );

    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;

}