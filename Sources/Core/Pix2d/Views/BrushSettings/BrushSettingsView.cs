using Pix2d.Shared;
using static Pix2d.Resources.StaticResources;
using Colors = Avalonia.Media.Colors;

namespace Pix2d.Views.BrushSettings;

public class BrushSettingsView : ComponentBase
{

    [Inject] private AppState AppState { get; set; } = null!;

    private DrawingState DrawingState => AppState.DrawingState;

    protected override object Build() =>
        new ScrollViewer()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Content(
                new Grid()
                    .Rows("Auto,*,64,64,64,Auto")
                    .Margin(8, 0)
                    .Children(
                        new TextBlock()
                            .Padding(4, 12, 0, 4)
                            .FontSize(12)
                            .Foreground(Colors.White.ToBrush())
                            .Text("Presets"),

                        new ListBox()
                            .Background("#414953".ToColor().ToBrush())
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Row(1)
                            .Padding(0)
                            .MinHeight(72)
                            .BorderThickness(1)
                            .Padding(4)
                            .ItemsSource(DrawingState.BrushPresets, bindingSource: DrawingState)
                            .SelectedItem(CurrentPixelBrushPreset, BindingMode.TwoWay, bindingSource: this)
                            .ItemsPanel(Templates.WrapPanelTemplate)
                            .ItemTemplate((Primitives.Drawing.BrushSettings item) => new BrushItemView().Preset(item)),

                        new SliderEx()
                            .Header("Size")
                            .Units("px")
                            .Minimum(1)
                            .Value(BrushScale, BindingMode.TwoWay, bindingSource: this)
                            .Row(2),

                        new SliderEx()
                            .Header("Opacity")
                            .Units("%")
                            .Value(BrushOpacity, BindingMode.TwoWay, bindingSource: this)
                            .Row(3),

                        new SliderEx()
                            .Header("Spacing")
                            .Units("px")
                            .Value(BrushSpacing, BindingMode.TwoWay, bindingSource: this)
                            .Row(4),

                        new ToggleSwitch()
                            .IsChecked(DrawingState.IsPixelPerfectDrawingModeEnabled, BindingMode.TwoWay, bindingSource: DrawingState)
                            .Row(5)
                    ));

    public Pix2d.Primitives.Drawing.BrushSettings CurrentPixelBrushPreset
    {
        get => DrawingState.CurrentPixelBrushPreset;
        set
        {
            DrawingState.CurrentPixelBrushPreset = value;

            if(value != null)
            {
                DrawingState.CurrentBrushSettings = value.Clone();
                OnPropertyChanged();
                OnPropertyChanged(nameof(BrushScale));
                OnPropertyChanged(nameof(BrushOpacity));
                OnPropertyChanged(nameof(BrushSpacing));
            }
        }
    }

    public float BrushScale
    {
        get => DrawingState.CurrentBrushSettings.Scale;
        set
        {
            var brush = DrawingState.CurrentBrushSettings.Clone();
            brush.Scale = value;

            if (brush.Equals(DrawingState.CurrentBrushSettings)) return;

            DrawingState.CurrentBrushSettings = brush;
            OnPropertyChanged();
        }
    }
    public float BrushOpacity
    {
        get => DrawingState.CurrentBrushSettings.Opacity * 100f;
        set
        {
            var brush = DrawingState.CurrentBrushSettings.Clone();
            brush.Opacity = value / 100f;

            if (brush.Equals(DrawingState.CurrentBrushSettings)) return;

            DrawingState.CurrentBrushSettings = brush;
            OnPropertyChanged();
        }
    }

    public float BrushSpacing
    {
        get => DrawingState.CurrentBrushSettings.Spacing;
        set
        {
            var brush = DrawingState.CurrentBrushSettings.Clone();
            brush.Spacing = value;
            if (brush.Equals(DrawingState.CurrentBrushSettings)) return;

            DrawingState.CurrentBrushSettings = brush;
            OnPropertyChanged();
        }
    }
}