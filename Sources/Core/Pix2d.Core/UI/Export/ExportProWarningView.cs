using Pix2d.Command;

namespace Pix2d.UI.Export;

public class ExportProWarningView : ComponentBase
{
    [Inject] private ICommandService CommandService { get; set; } = null!;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;

    protected override object Build() =>
        new Button().Background(Brushes.DeepSkyBlue)
            .Foreground(Brushes.White)
            .VerticalAlignment(VerticalAlignment.Top)
            // .IsHitTestVisible(false)
            .Command(ViewCommands.ShowLicensePurchaseCommand)
            .Content(
                new TextBlock()
                    .Text("Get PRO version now to disable Pix2d watermark")
                    .TextWrapping(TextWrapping.Wrap)
                    .FontSize(14)
            );
}