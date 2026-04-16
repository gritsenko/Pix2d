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

    private Button _button = null!;

    public event EventHandler<ColorChangedEventArgs>? ColorChanged;

    protected override object Build() =>
        new Button()
            .Ref(out _button)
            .Width(30)
            .Height(20)
            .Background(Color.ToBrush())
            .BorderThickness(1)
            .BorderBrush(Brushes.Gray)
            .Flyout(
                new Flyout()
                    .Content(
                        new Pix2dColorPicker().Row(1)
                            .Margin(0, 8)
                            .Color(this, x => x.Color, BindingMode.TwoWay)
                            .Width(200)
                            .Height(140)
                    )
            );

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ColorProperty && _button != null)
        {
            _button.Background = Color.ToBrush();

            if (change.OldValue is SKColor oldColor && change.NewValue is SKColor newColor
                && change.OldValue != change.NewValue)
            {
                ColorChanged?.Invoke(this, new ColorChangedEventArgs((SKColor)change.OldValue, (SKColor)change.NewValue));
            }
        }
    }
}