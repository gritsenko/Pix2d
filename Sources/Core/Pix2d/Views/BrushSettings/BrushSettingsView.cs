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
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Row(1)
                            .Padding(0)
                            .MinHeight(72)
                            .BorderThickness(1)
                            .Padding(4)
                            .ItemsSource(DrawingState.BrushPresets, bindingSource: DrawingState)
                            .SelectedItem(DrawingState.CurrentPixelBrushPreset, BindingMode.TwoWay, bindingSource: DrawingState)
                            .ItemsPanel(Templates.WrapPanelTemplate)
                            .ItemTemplate((Primitives.Drawing.BrushSettings item) => new BrushItemView().Preset(item)),

                        new SliderEx()
                            .Header("Size")
                            .Units("px")
                            .Minimum(1)
                            .Value(BrushScale, BindingMode.TwoWay, bindingSource: DrawingState)
                            .Row(2),

                        new SliderEx()
                            .Header("Opacity")
                            .Units("%")
                            .Value(DrawingState.CurrentBrushSettings.Opacity, BindingMode.TwoWay, bindingSource: DrawingState.CurrentBrushSettings)
                            .Row(3),

                        new SliderEx()
                            .Header("Spacing")
                            .Units("px")
                            .Value(DrawingState.CurrentBrushSettings.Spacing, BindingMode.TwoWay, bindingSource: DrawingState.CurrentBrushSettings)
                            .Row(4),

                        new ToggleSwitch()
                            .IsChecked(DrawingState.IsPixelPerfectDrawingModeEnabled, BindingMode.TwoWay, bindingSource: DrawingState)
                            .Row(5)
                    ));


    public float BrushScale
    {
        get => DrawingState.CurrentBrushSettings.Scale;
        set
        {
            if (value.Equals(DrawingState.CurrentBrushSettings.Scale)) return;
            DrawingState.CurrentBrushSettings.Scale = value;
            OnPropertyChanged();
        }
    }
}