using System.Globalization;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public enum SliderExLayoutMode
{
    TwoLine,
    OneLine,
}

public enum SliderExNarrowMode
{
    None,
    PopupEditor,
}

public class SliderEx : ViewBase
{
    private const double NarrowWindowThreshold = 500d;

    #region AvaloniaProperties
    public static readonly DirectProperty<SliderEx, double> ValueProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Value), o => o.Value, (o, v) => o.Value = v);
    private double _value;
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
    private string _units = string.Empty;
    public string Units
    {
        get => _units;
        set => SetAndRaise(UnitsProperty, ref _units, value);
    }

    public static readonly DirectProperty<SliderEx, double> MinimumProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(Minimum), o => o.Minimum, (o, v) => o.Minimum = v);
    private double _minimum;
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

    public static readonly DirectProperty<SliderEx, SliderExLayoutMode> LayoutModeProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, SliderExLayoutMode>(nameof(LayoutMode), o => o.LayoutMode, (o, v) => o.LayoutMode = v);
    private SliderExLayoutMode _layoutMode = SliderExLayoutMode.TwoLine;
    public SliderExLayoutMode LayoutMode
    {
        get => _layoutMode;
        set => SetAndRaise(LayoutModeProperty, ref _layoutMode, value);
    }

    public static readonly DirectProperty<SliderEx, SliderExNarrowMode> NarrowModeProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, SliderExNarrowMode>(nameof(NarrowMode), o => o.NarrowMode, (o, v) => o.NarrowMode = v);
    private SliderExNarrowMode _narrowMode;
    public SliderExNarrowMode NarrowMode
    {
        get => _narrowMode;
        set => SetAndRaise(NarrowModeProperty, ref _narrowMode, value);
    }

    public static readonly DirectProperty<SliderEx, double> NarrowWidthThresholdProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, double>(nameof(NarrowWidthThreshold), o => o.NarrowWidthThreshold, (o, v) => o.NarrowWidthThreshold = v);
    private double _narrowWidthThreshold = 260d;
    public double NarrowWidthThreshold
    {
        get => _narrowWidthThreshold;
        set => SetAndRaise(NarrowWidthThresholdProperty, ref _narrowWidthThreshold, value);
    }

    #endregion

    public event Action<double>? ValueChanged;

    protected override object Build() =>
        new Grid()
            .Ref(out _root)
            .OnSizeChanged(_ => UpdateVisualState())
            .Children(
                BuildTwoLineLayout(),
                BuildOneLineLayout(),
                BuildNarrowLayout());

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachTopLevel();
        UpdateVisualState();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachTopLevel();
        base.OnDetachedFromVisualTree(e);
    }

    private Grid _root = null!;
    private Grid _twoLineLayout = null!;
    private TextBlock _twoLineLabelTextBlock = null!;
    private NumericUpDown _twoLineNumericUpDown = null!;
    private TextBlock _twoLineUnitsTextBlock = null!;
    private Slider _twoLineSlider = null!;
    private Grid _oneLineLayout = null!;
    private TextBlock _oneLineLabelTextBlock = null!;
    private NumericUpDown _oneLineNumericUpDown = null!;
    private TextBlock _oneLineUnitsTextBlock = null!;
    private Slider _oneLineSlider = null!;
    private Button _narrowButton = null!;
    private TextBlock _narrowValueTextBlock = null!;
    private TextBlock _popupLabelTextBlock = null!;
    private NumericUpDown _popupNumericUpDown = null!;
    private TextBlock _popupUnitsTextBlock = null!;
    private Slider _popupSlider = null!;
    private TopLevel? _topLevel;

    private Grid BuildTwoLineLayout() =>
        new Grid()
            .Ref(out _twoLineLayout)
            .Rows("Auto,Auto")
            .Cols("Auto,*,Auto")
            .Margin(0, 4)
            .IsVisible(LayoutMode == SliderExLayoutMode.TwoLine)
            .Children(
                new TextBlock()
                    .Ref(out _twoLineLabelTextBlock)
                    .Text(Label)
                    .VerticalAlignment(VerticalAlignment.Center),
                CreateNumericUpDown(out _twoLineNumericUpDown, OnNumericValueChanged)
                    .Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Right),
                new TextBlock()
                    .Ref(out _twoLineUnitsTextBlock)
                    .Text(Units)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(4)
                    .Col(2),
                CreateSlider(out _twoLineSlider, OnSliderValueChanged)
                    .Row(1)
                    .ColSpan(3));

    private Grid BuildOneLineLayout() =>
        new Grid()
            .Ref(out _oneLineLayout)
            .Cols("Auto,*,Auto,Auto")
            .Margin(0, 4)
            .IsVisible(LayoutMode == SliderExLayoutMode.OneLine)
            .Children(
                new TextBlock()
                    .Ref(out _oneLineLabelTextBlock)
                    .Text(Label)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(0, 0, 12, 0),
                CreateSlider(out _oneLineSlider, OnSliderValueChanged)
                    .Col(1)
                    .VerticalAlignment(VerticalAlignment.Center),
                CreateNumericUpDown(out _oneLineNumericUpDown, OnNumericValueChanged)
                    .Col(2)
                    .Margin(12, 0, 0, 0),
                new TextBlock()
                    .Ref(out _oneLineUnitsTextBlock)
                    .Text(Units)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(4, 0, 0, 0)
                    .Col(3));

    private Button BuildNarrowLayout() =>
        new Button()
            .Ref(out _narrowButton)
            .Margin(0, 4)
            .Padding(new Thickness(0))
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Background(Avalonia.Media.Brushes.Transparent)
            .BorderThickness(new Thickness(0))
            .IsVisible(false)
            .With(button =>
            {
                var flyout = new Flyout() { Placement = PlacementMode.Bottom };
                button.Click += (_, _) => flyout.ShowAt(button);
                flyout.Content = BuildNarrowFlyoutContent();
            })
            .Content(
                new Border()
                    .Padding(new Thickness(12, 6))
                    .Background(StaticResources.Brushes.InnerPanelBackgroundBrush)
                    .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
                    .BorderThickness(new Thickness(1))
                    .CornerRadius(new CornerRadius(8))
                    .Child(
                        new TextBlock()
                            .Ref(out _narrowValueTextBlock)
                            .MinWidth(52)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .Text(GetNarrowValueText())
                    ));

    private Control BuildNarrowFlyoutContent() =>
        new Border()
            .Padding(new Thickness(12))
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
            .BorderThickness(new Thickness(1))
            .CornerRadius(new CornerRadius(12))
            .Child(
                new StackPanel()
                    .Spacing(12)
                    .Children(
                        new TextBlock()
                            .Ref(out _popupLabelTextBlock)
                            .Text(Label)
                            .IsVisible(!string.IsNullOrWhiteSpace(Label)),
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Spacing(8)
                            .Children(
                                CreateNumericUpDown(out _popupNumericUpDown, OnNumericValueChanged),
                                new TextBlock()
                                    .Ref(out _popupUnitsTextBlock)
                                    .Text(Units)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),
                        CreateSlider(out _popupSlider, OnSliderValueChanged)
                            .Width(220)
                    ));

    private NumericUpDown CreateNumericUpDown(out NumericUpDown numericUpDown, Action<decimal?> valueChanged) =>
        new NumericUpDown()
            .Ref(out numericUpDown)
            .Width(80)
            .Minimum((decimal)Minimum)
            .Maximum((decimal)Maximum)
            .NumberFormat(new NumberFormatInfo() { NumberDecimalDigits = 0 })
            .Increment(1)
            .Value((decimal)Value)
            .OnValueChanged(e => valueChanged(e.NewValue));

    private Slider CreateSlider(out Slider slider, Action<double> valueChanged) =>
        new Slider()
            .Ref(out slider)
            .TickFrequency(1)
            .IsSnapToTickEnabled(true)
            .Maximum(Maximum)
            .Minimum(Minimum)
            .SmallChange(1)
            .LargeChange(10)
            .Value(Value)
            .OnValueChanged(e => valueChanged(e.NewValue));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_root == null)
            return;

        if (change.Property == LabelProperty)
            UpdateLabels();
        else if (change.Property == UnitsProperty)
            UpdateUnits();
        else if (change.Property == MinimumProperty)
            UpdateMinimum();
        else if (change.Property == MaximumProperty)
            UpdateMaximum();
        else if (change.Property == ValueProperty)
            UpdateValue();
        else if (change.Property == LayoutModeProperty || change.Property == NarrowModeProperty || change.Property == NarrowWidthThresholdProperty)
            UpdateVisualState();
    }

    private void UpdateLabels()
    {
        _twoLineLabelTextBlock.Text = Label;
        _oneLineLabelTextBlock.Text = Label;
        _popupLabelTextBlock.Text = Label;
        _popupLabelTextBlock.IsVisible = !string.IsNullOrWhiteSpace(Label);
    }

    private void UpdateUnits()
    {
        _twoLineUnitsTextBlock.Text = Units;
        _oneLineUnitsTextBlock.Text = Units;
        _popupUnitsTextBlock.Text = Units;
        _narrowValueTextBlock.Text = GetNarrowValueText();
    }

    private void UpdateMinimum()
    {
        UpdateNumericRange(n => n.Minimum = (decimal)Minimum);
        UpdateSliderRange(s => s.Minimum = Minimum);
    }

    private void UpdateMaximum()
    {
        UpdateNumericRange(n => n.Maximum = (decimal)Maximum);
        UpdateSliderRange(s => s.Maximum = Maximum);
    }

    private void UpdateValue()
    {
        var decimalValue = (decimal)Value;

        UpdateNumericRange(n =>
        {
            if (n.Value != decimalValue)
                n.Value = decimalValue;
        });

        UpdateSliderRange(s =>
        {
            if (s.Value != Value)
                s.Value = Value;
        });

        _narrowValueTextBlock.Text = GetNarrowValueText();
    }

    private void UpdateNumericRange(Action<NumericUpDown> update)
    {
        update(_twoLineNumericUpDown);
        update(_oneLineNumericUpDown);
        update(_popupNumericUpDown);
    }

    private void UpdateSliderRange(Action<Slider> update)
    {
        update(_twoLineSlider);
        update(_oneLineSlider);
        update(_popupSlider);
    }

    private void UpdateVisualState()
    {
        if (_twoLineLayout == null || _oneLineLayout == null || _narrowButton == null)
            return;

        var actualWidth = GetActualWidth();
        var isControlNarrow = actualWidth > 0 && actualWidth <= NarrowWidthThreshold;
        var isWindowNarrow = IsWindowNarrow();
        var isNarrow = NarrowMode == SliderExNarrowMode.PopupEditor && (isControlNarrow || isWindowNarrow);

        _twoLineLayout.IsVisible = !isNarrow && LayoutMode == SliderExLayoutMode.TwoLine;
        _oneLineLayout.IsVisible = !isNarrow && LayoutMode == SliderExLayoutMode.OneLine;
        _narrowButton.IsVisible = isNarrow;
    }

    private double GetActualWidth() => _root.Bounds.Width > 0 ? _root.Bounds.Width : Bounds.Width;

    private bool IsWindowNarrow()
    {
        var topLevelWidth = _topLevel?.Bounds.Width ?? TopLevel.GetTopLevel(this)?.Bounds.Width ?? 0;
        return topLevelWidth > 0 && topLevelWidth <= NarrowWindowThreshold;
    }

    private void AttachTopLevel()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(_topLevel, topLevel))
            return;

        DetachTopLevel();
        _topLevel = topLevel;

        if (_topLevel != null)
            _topLevel.SizeChanged += TopLevelOnSizeChanged;
    }

    private void DetachTopLevel()
    {
        if (_topLevel == null)
            return;

        _topLevel.SizeChanged -= TopLevelOnSizeChanged;
        _topLevel = null;
    }

    private void TopLevelOnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateVisualState();

    private string GetNarrowValueText()
    {
        var valueText = ((int)Math.Round(Value)).ToString();
        return string.IsNullOrWhiteSpace(Units) ? valueText : $"{valueText} {Units}";
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