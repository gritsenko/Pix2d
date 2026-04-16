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

    public event Action<double>? ValueChanged;

    protected override object Build() =>
        new Grid()
            .Rows("Auto,Auto")
            .Cols("Auto,*, 20")
            .Margin(0, 4)
            .Children(
                new TextBlock()
                    .Ref(out _labelTextBlock)
                    .Text(Label)
                    .VerticalAlignment(VerticalAlignment.Center),

                new NumericUpDown()
                    .Ref(out _numericUpDown)
                    .Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .Width(80)
                    .Minimum((decimal)Minimum)
                    .Maximum((decimal)Maximum)
                    .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
                    .Increment(1)
                    .Value((decimal)Value)
                    .OnValueChanged(e => OnNumericValueChanged(e.NewValue)),
                    
                new TextBlock()
                    .Ref(out _unitsTextBlock)
                    .Text(Units)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(4)
                    .Col(2),

                new Slider()
                    .Ref(out _slider)
                    .Row(1)
                    .ColSpan(3)
                    .TickFrequency(1)
                    .IsSnapToTickEnabled(true)
                    .Maximum(Maximum)
                    .Minimum(Minimum)
                    .SmallChange(1)
                    .LargeChange(10)
                    .Value(Value)
                    .OnValueChanged(e => OnSliderValueChanged(e.NewValue))
            );

    private TextBlock _labelTextBlock = null!;
    private NumericUpDown _numericUpDown = null!;
    private TextBlock _unitsTextBlock = null!;
    private Slider _slider = null!;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_labelTextBlock == null || _numericUpDown == null || _unitsTextBlock == null || _slider == null)
            return;

        if (change.Property == LabelProperty)
            _labelTextBlock.Text = Label;
        else if (change.Property == UnitsProperty)
            _unitsTextBlock.Text = Units;
        else if (change.Property == MinimumProperty)
        {
            _numericUpDown.Minimum = (decimal)Minimum;
            _slider.Minimum = Minimum;
        }
        else if (change.Property == MaximumProperty)
        {
            _numericUpDown.Maximum = (decimal)Maximum;
            _slider.Maximum = Maximum;
        }
        else if (change.Property == ValueProperty)
        {
            var decimalValue = (decimal)Value;
            if (_numericUpDown.Value != decimalValue)
                _numericUpDown.Value = decimalValue;
            if (_slider.Value != Value)
                _slider.Value = Value;
        }
    }

    private void OnNumericValueChanged(decimal? value)
    {
        var nextValue = (double)(value ?? 0m);
        if (Value != nextValue)
        {
            Value = nextValue;
            ValueChanged?.Invoke(nextValue);
        }
    }

    private void OnSliderValueChanged(double value)
    {
        if (Value != value)
        {
            Value = value;
            ValueChanged?.Invoke(value);
        }
    }
}