using System.Globalization;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

/// <summary>
/// Numeric slider styled after the Pix2D redesign: a rounded track whose accent fill grows left→right
/// with the value, the label on top of the current value inside the box, and the units on the right.
///
/// Interaction (mirrors the color-picker hue bar, <see cref="Pix2dColorPicker"/>):
/// • press + drag anywhere on the track scrubs the value (absolute, like a slider);
/// • a click without drag turns the value into a keyboard text field — Enter / focus-loss commits,
///   Esc or the ✕ button cancels;
/// • the mouse wheel nudges the value while hovering (1 step, Ctrl = 10) — issue #242.
/// </summary>
public class SliderEx : ViewBase
{
    private const double SliderSmallChange = 1d;
    private const double SliderLargeChange = 10d;
    // Pointer travel (px) that separates a "click" (→ text edit) from a "drag" (→ scrub).
    private const double DragThreshold = 4d;

    private static readonly FontFamily Font = StaticResources.Fonts.DefaultTextFontFamily;

    // The accent fill is a background band; the label / value / units always stay in the light
    // foreground tiers on top of it (the dark stays the track's *background*, never painted over text).
    private static readonly IBrush LabelBrush = StaticResources.Brushes.SecondaryForegroundBrush;
    private static readonly IBrush ValueBrush = StaticResources.Brushes.ForegroundBrush;
    private static readonly IBrush UnitsBrush = StaticResources.Brushes.SecondaryForegroundBrush;

    // Accent fill: the redesign's orange→amber gradient, horizontal (matches AccentBrush / SelectedToolBrush).
    private static readonly IBrush AccentFillBrush =
        new LinearGradientBrush()
            .EndPoint(new Point(1, 0), RelativeUnit.Relative)
            .GradientStops([new GradientStop("#FF6B00".ToColor(), 0), new GradientStop("#E5B407".ToColor(), 1)]);

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
    private string _label = "LABEL";
    public string Label
    {
        get => _label;
        // The design renders all captions uppercase (Figma textCase: UPPER); Avalonia has no
        // text-transform, so normalize here.
        set => SetAndRaise(LabelProperty, ref _label, value?.ToUpperInvariant()!);
    }

    public static readonly DirectProperty<SliderEx, string> UnitsProperty
        = AvaloniaProperty.RegisterDirect<SliderEx, string>(nameof(Units), o => o.Units, (o, v) => o.Units = v);
    private string _units = string.Empty;
    public string Units
    {
        get => _units;
        set => SetAndRaise(UnitsProperty, ref _units, value?.ToUpperInvariant()!);
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
    #endregion

    public event Action<double>? ValueChanged;

    // Shared corner radius for the track, the clip and the accent focus frame — keep them equal so the
    // frame rounds exactly along the track's rounded rect (otherwise the corners look "cut").
    private const double CornerRadiusValue = 10d;

    private Border _track = null!;
    private Border _fill = null!;
    private Border _focusFrame = null!;
    private TextBlock _labelLight = null!;
    private TextBlock _valueLight = null!;
    private TextBlock _unitsLight = null!;
    private Grid _editOverlay = null!;
    private TextBlock _editLabel = null!;
    private TextBox _editBox = null!;

    private bool _isPointerDown;
    private bool _isDragging;
    private bool _isEditing;
    private double _pressX;

    protected override object Build() =>
        new Border()
            .Ref(out _track)
            .Margin(0, 3)
            .MinHeight(36)
            .CornerRadius(new CornerRadius(CornerRadiusValue))
            .Background(StaticResources.Brushes.InnerPanelBackgroundBrush)
            .ClipToBounds(true)
            .OnSizeChanged(_ => UpdateFill())
            .Child(
                new Panel()
                    .Children(
                        // 1. Accent fill (background band), its width proportional to the value.
                        new Border()
                            .Ref(out _fill)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Background(AccentFillBrush)
                            .Width(0),

                        // 2. Label / value / units, always in the light foreground over the fill.
                        CreateContentGrid(LabelBrush, ValueBrush, UnitsBrush,
                            out _labelLight, out _valueLight, out _unitsLight),

                        // 3. Keyboard-entry overlay, shown on click.
                        BuildEditOverlay(),

                        // 4. Accent focus frame — top-most so the edit overlay's opaque background can't
                        //    paint over its rounded corners. Same radius as the track → corners round
                        //    together with the control instead of being clipped square.
                        new Border()
                            .Ref(out _focusFrame)
                            .IsVisible(false)
                            .IsHitTestVisible(false)
                            .CornerRadius(new CornerRadius(CornerRadiusValue))
                            .BorderThickness(new Thickness(1))
                            .BorderBrush(StaticResources.Brushes.AccentBrush)
                            .Background(Avalonia.Media.Brushes.Transparent)));

    private Grid CreateContentGrid(IBrush labelBrush, IBrush valueBrush, IBrush unitsBrush,
        out TextBlock labelTb, out TextBlock valueTb, out TextBlock unitsTb) =>
        new Grid()
            .Cols("*,Auto")
            .Children(
                new StackPanel()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(12, 0, 0, 0)
                    .Spacing(0)
                    .Children(
                        new TextBlock()
                            .Ref(out labelTb)
                            .FontFamily(Font)
                            .FontSize(8)
                            .Foreground(labelBrush)
                            .Text(Label),
                        new TextBlock()
                            .Ref(out valueTb)
                            .FontFamily(Font)
                            .FontSize(16)
                            .Foreground(valueBrush)
                            .Text(FormatValue())),
                new TextBlock()
                    .Ref(out unitsTb)
                    .Col(1)
                    .FontFamily(Font)
                    .FontSize(13)
                    .Foreground(unitsBrush)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(0, 0, 12, 0)
                    .Text(Units));

    private Grid BuildEditOverlay() =>
        new Grid()
            .Ref(out _editOverlay)
            .Cols("*,Auto")
            .IsVisible(false)
            .Background(StaticResources.Brushes.InnerPanelBackgroundBrush)
            .Children(
                new StackPanel()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(12, 0, 0, 0)
                    .Spacing(0)
                    .Children(
                        new TextBlock()
                            .Ref(out _editLabel)
                            .FontFamily(Font)
                            .FontSize(8)
                            .Foreground(LabelBrush)
                            .Text(Label),
                        new TextBox()
                            .Ref(out _editBox)
                            .FontFamily(Font)
                            .FontSize(16)
                            .Foreground(ValueBrush)
                            .CaretBrush(StaticResources.Brushes.ForegroundBrush)
                            .Background(Avalonia.Media.Brushes.Transparent)
                            .BorderThickness(new Thickness(0))
                            .Padding(new Thickness(0))
                            .MinWidth(40)
                            .VerticalContentAlignment(VerticalAlignment.Center)),
                new Button()
                    .Col(1)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(0, 0, 6, 0)
                    .Width(24)
                    .Height(24)
                    .MinWidth(0)
                    .MinHeight(0)
                    .Padding(new Thickness(0))
                    .CornerRadius(new CornerRadius(8))
                    .Background(Avalonia.Media.Brushes.Transparent)
                    .ToolTip_Tip(L("Cancel"))
                    .OnClick(_ => CancelEdit())
                    .Content(
                        new TextBlock()
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .FontSize(11)
                            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
                            .Text("") // Segoe MDL2 close
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)));

    protected override void OnAfterInitialized()
    {
        _track.PointerPressed += OnTrackPointerPressed;
        _track.PointerMoved += OnTrackPointerMoved;
        _track.PointerReleased += OnTrackPointerReleased;
        _track.PointerCaptureLost += OnTrackPointerCaptureLost;
        // Desktop convenience: adjust with the mouse wheel while hovering (issue #242). One notch = 1
        // step, Ctrl = 10. Marked handled so a parent ScrollViewer (tool panels scroll) doesn't scroll.
        _track.PointerWheelChanged += OnTrackPointerWheel;
        _track.Cursor = new Cursor(StandardCursorType.SizeWestEast);

        _editBox.KeyDown += OnEditBoxKeyDown;
        _editBox.LostFocus += OnEditBoxLostFocus;

        UpdateFill();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_track == null)
            return;

        if (change.Property == LabelProperty)
            UpdateLabels();
        else if (change.Property == UnitsProperty)
            UpdateUnits();
        else if (change.Property == ValueProperty)
            UpdateValue();
        else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
            UpdateFill();
    }

    private void UpdateLabels()
    {
        _labelLight.Text = Label;
        _editLabel.Text = Label;
    }

    private void UpdateUnits()
    {
        _unitsLight.Text = Units;
    }

    private void UpdateValue()
    {
        _valueLight.Text = FormatValue();
        UpdateFill();
    }

    /// <summary>Resizes the accent fill (and the clipped dark-text copy) to reflect the current value.</summary>
    private void UpdateFill()
    {
        if (_track == null)
            return;

        var width = _track.Bounds.Width;
        var range = Maximum - Minimum;
        var ratio = range > 0 ? (Value - Minimum) / range : 0d;
        ratio = Math.Clamp(ratio, 0d, 1d);

        _fill.Width = ratio * width;
    }

    private string FormatValue() => ((int)Math.Round(Value)).ToString(CultureInfo.InvariantCulture);

    #region Pointer scrubbing (mirrors Pix2dColorPicker's hue bar)

    private void OnTrackPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isEditing)
            return;

        var point = e.GetCurrentPoint(_track);
        if (!(point.Properties.IsLeftButtonPressed || e.Pointer.Type == PointerType.Touch))
            return;

        e.Pointer.Capture(_track);
        _pressX = point.Position.X;
        _isPointerDown = true;
        _isDragging = false;
        e.Handled = true;
    }

    private void OnTrackPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || e.Pointer.Captured != _track)
            return;

        var x = e.GetCurrentPoint(_track).Position.X;
        if (!_isDragging && Math.Abs(x - _pressX) > DragThreshold)
            _isDragging = true;

        if (_isDragging)
        {
            SetValueFromX(x);
            e.Handled = true;
        }
    }

    private void OnTrackPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPointerDown)
            return;

        var wasDragging = _isDragging;
        _isPointerDown = false;
        _isDragging = false;

        if (e.Pointer.Captured == _track)
            e.Pointer.Capture(null);

        // A press that never crossed the drag threshold is a click → open the keyboard editor.
        if (!wasDragging)
            EnterEditMode();

        e.Handled = true;
    }

    private void OnTrackPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _isPointerDown = false;
        _isDragging = false;
    }

    private void OnTrackPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.Delta.Y == 0)
            return;

        var step = (e.KeyModifiers & KeyModifiers.Control) != 0 ? SliderLargeChange : SliderSmallChange;
        var direction = e.Delta.Y > 0 ? 1d : -1d;
        CommitValue(Value + direction * step);
        e.Handled = true;
    }

    private void SetValueFromX(double x)
    {
        var width = _track.Bounds.Width;
        if (width <= 0)
            return;

        var ratio = Math.Clamp(x / width, 0d, 1d);
        CommitValue(Minimum + ratio * (Maximum - Minimum));
    }

    private void CommitValue(double value)
    {
        var clamped = Math.Clamp(Math.Round(value), Minimum, Maximum);
        if (clamped == Value)
            return;

        Value = clamped; // setter raises ValueProperty → UpdateValue() refreshes text + fill
        ValueChanged?.Invoke(Value);
    }

    #endregion

    #region Keyboard editing

    private void EnterEditMode()
    {
        if (_isEditing)
            return;

        _isEditing = true;
        _editBox.Text = FormatValue();
        _editOverlay.IsVisible = true;
        _focusFrame.IsVisible = true;

        // Focus + select-all once the overlay is realized, so the current value is ready to overtype.
        Dispatcher.UIThread.Post(() =>
        {
            _editBox.Focus();
            _editBox.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnEditBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    // Clicking away from the editor commits, matching a plain text field.
    private void OnEditBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_isEditing)
            CommitEdit();
    }

    private void CommitEdit()
    {
        var text = _editBox.Text;
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ||
            double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            CommitValue(parsed);
        }

        ExitEditMode();
    }

    private void CancelEdit() => ExitEditMode();

    private void ExitEditMode()
    {
        if (!_isEditing)
            return;

        _isEditing = false;
        _editOverlay.IsVisible = false;
        _focusFrame.IsVisible = false;
    }

    #endregion
}
