namespace Pix2d.Shared;

public class SliderEx : ViewBase
{

    #region AvaloniaProperties
    public static readonly DirectProperty<SliderEx, double> ValueProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Value), o => o.Value, (o, v) => o.Value = v);
    private double _value = 0d;
    public double Value
    {
        get => _value;
        set => SetAndRaise(ValueProperty, ref _value, value);
    }

    public static readonly DirectProperty<SliderEx, string> HeaderProperty
    = AvaloniaProperty.RegisterDirect<SliderEx, string>(nameof(Header), o => o.Header, (o, v) => o.Header = v);
    private string _header = "Header";
    public string Header
    {
        get => _header;
        set => SetAndRaise(HeaderProperty, ref _header, value);
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
                    .Text(HeaderProperty, BindingMode.OneWay)
                    .VerticalAlignment(VerticalAlignment.Center),

                new NumericUpDown()
                    .Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Width(80)
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