namespace Pix2d.UI.Export;

public class ExportProWarningView : ComponentBase
{
    protected override object Build() =>
        new Button().Background(Brushes.DeepSkyBlue)
            .Foreground(Brushes.White)
            .VerticalAlignment(VerticalAlignment.Top)
            .IsHitTestVisible(false)
            .Content(
                new TextBlock()
                    .Text("To disable Pix2d watermark, please accrue PRO version")
                    .TextWrapping(TextWrapping.Wrap)
                    .FontSize(14)
            );
}