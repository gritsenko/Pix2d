using System.Globalization;

namespace Pix2d.UI.Shared;

public class SliderEx : ViewBase
{

    #region AvaloniaProperties
    public static readonly DirectProperty<SliderEx, double> ValueProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Value), o => o.Value, (o, v) => o.Value = v);
    private double _value = 0d;
    public double Value
    {
        get => _value;
        set
        {
            var rounded = Math.Round(value);
            SetAndRaise(ValueProperty, ref _value, rounded);
        }
    }

    public static readonly DirectProperty<SliderEx, string> LabelProperty
    = AvaloniaProperty.RegisterDirect<SliderEx, string>(nameof(Label), o => o.Label, (o, v) => o.Label = v);
    private string _label = "Label";
    public string Label
    {
        get => _label;
        set => SetAndRaise(LabelProperty, ref _label, value);
    }

    public static readonly DirectProperty<SliderEx, string> UnitsProperty
    = AvaloniaProperty.RegisterDirect<SliderEx, string>(nameof(Units), o => o.Units, (o, v) => o.Units = v);
    private string _units = "";
    public string Units
    {
        get => _units;
        set => SetAndRaise(UnitsProperty, ref _units, value);
    }

    public static readonly DirectProperty<SliderEx, double> MinimumProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Minimum), o => o.Minimum, (o, v) => o.Minimum = v);
    private double _minimum = 0d;
    public double Minimum
    {
        get => _minimum;
        set => SetAndRaise(MinimumProperty, ref _minimum, value);
    }

    public static readonly DirectProperty<SliderEx, double> MaximumProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Maximum), o => o.Maximum, (o, v) => o.Maximum = v);
    private double _maximum = 100d;
    public double Maximum
    {
        get => _maximum;
        set => SetAndRaise(MaximumProperty, ref _maximum, value);
    }

    #endregion

    protected override object Build() =>
        new Grid()
            .Rows("Auto,Auto")
            .Cols("Auto,*, 20")
            .Margin(0, 4)
            .Children(
                new TextBlock()
                    .Text(LabelProperty, BindingMode.OneWay)
                    .VerticalAlignment(VerticalAlignment.Center),

                new NumericUpDown()
                    .Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Width(80)
                    .Minimum(MinimumProperty)
                    .Maximum(MaximumProperty)
                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                    .Increment(1)
                    .Value(ValueProperty, BindingMode.TwoWay),

                new TextBlock()
                    .Text(UnitsProperty, BindingMode.TwoWay)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(4)
                    .Col(2),

                new Slider()
                    .Row(1)
                    .ColSpan(3)
                    .TickFrequency(1)
                    .IsSnapToTickEnabled(true)
                    .Maximum(MaximumProperty)
                    .Minimum(MinimumProperty)
                    .SmallChange(1)
                    .LargeChange(10)
                    .Value(ValueProperty, BindingMode.TwoWay)
            );
}