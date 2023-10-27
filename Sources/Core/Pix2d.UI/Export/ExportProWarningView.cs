namespace Pix2d.UI.Export;

public class ExportProWarningView : ComponentBase
{
    protected override object Build() =>
        new Button().Background(Brushes.DeepSkyBlue)
            .Foreground(Brushes.White)
            .VerticalAlignment(VerticalAlignment.Top)
            // .IsHitTestVisible(false)
            .Command(Commands.View.ShowLicensePurchaseCommand)
            .Content(
                new TextBlock()
                    .Text("Get PRO version now to disable Pix2d watermark")
                    .TextWrapping(TextWrapping.Wrap)
                    .FontSize(14)
            );
}