namespace Pix2d.Shared;

public class ColorPickerButton : ViewBase
{
    protected override object Build() =>
        new Button()
            .Width(30)
            .Height(20)
            .Background(Brushes.Red);
}