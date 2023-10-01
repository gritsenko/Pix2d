using Avalonia;

namespace Pix2d.UI;

public class InfoPanelViewEx : ComponentBase
{
    private void LabelStyle(TextBlock t) => t
        .VerticalAlignment(VerticalAlignment.Center)
        .FontSize(12);

    protected override object Build() =>
        new Grid()
            .Cols("28,70,24,*")
            .IsHitTestVisible(false)
            .Height(28)
            .Children(

                new TextBlock().ColSpan(3)
                    .Text("Hello world 111")
                    .Padding(8, 0, 0, 0)
                    .With(LabelStyle)
            );

}