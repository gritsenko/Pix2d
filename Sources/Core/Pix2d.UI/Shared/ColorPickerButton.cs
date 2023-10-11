using Pix2d.UI.Common.Extensions;
using SkiaSharp;

namespace Pix2d.UI.Shared;

public class ColorPickerButton : ViewBase
{
    public static readonly DirectProperty<ColorPickerButton, SKColor> ColorProperty
        = AvaloniaProperty.RegisterDirect<ColorPickerButton, SKColor>(nameof(Color), o => o.Color, (o, v) => o.Color = v);
    private SKColor _color = SKColors.Red;
    public SKColor Color
    {
        get => _color;
        set => SetAndRaise(ColorProperty, ref _color, value);
    }

    protected override object Build() =>
        new Button()
            .Width(30)
            .Height(20)
            .Background(Color, new FuncValueConverter<SKColor, IBrush>(v => v.ToBrush()), BindingMode.OneWay, this)
            .Flyout(
                new Flyout()
                    .Content(
                        new Pix2dColorPicker().Row(1)
                            .Margin(10)
                            .Color(Color, BindingMode.TwoWay, bindingSource: this)
                            .Margin(0, 8)
                            .Width(200)
                            .Height(140)
                    )
            );
}