using Avalonia.Styling;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Colors = Avalonia.Media.Colors;

namespace Pix2d.UI.BrushSettings;

public class BrushSettingsView : LocalizedComponentBase
{
    [Inject] private AppState AppState { get; set; } = null!;

    private SpriteEditorState DrawingState => AppState.SpriteEditorState;

    public BrushSettingsView()
    {
        DrawingState.WatchFor(x => x.CurrentBrushSettings, UpdateSliders);
    }

    protected override StyleGroup? BuildStyles() => [
        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>())
            .Background(StaticResources.Brushes.BrushItemBrush)
            .Margin(2)
            .Width(44)
            .Height(44)
            .CornerRadius(12)
    ];

    protected override object Build() =>
        new ScrollViewer()
            .Content(
                new Grid()
                    .Rows("Auto,*,64,64,64,Auto")
                    .Margin(8, 0)
                    .Children(
                        new TextBlock()
                            .Padding(4, 12, 0, 4)
                            .FontSize(12)
                            .Foreground(Colors.White.ToBrush())
                            .Text(L("Presets")),

                        new ListBox()
                            .Background(Avalonia.Media.Brushes.Transparent)
                            .ScrollViewer_HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Row(1)
                            .Padding(0)
                            .MinHeight(72)
                            .BorderThickness(0)
                            .Padding(0)
                            .ItemsSource(DrawingState.BrushPresets, bindingSource: DrawingState)
                            .SelectedItem(CurrentPixelBrushPreset, BindingMode.TwoWay, bindingSource: this)
                            .ItemsPanel(StaticResources.Templates.WrapPanelTemplate)
                            .ItemTemplate((Primitives.Drawing.BrushSettings itemVm) =>
                                new BrushItemView()
                                            .Preset(itemVm)
                                            .ShowSizeText(true)
                                    ),

                        new SliderEx()
                            .Label(L("Size"))
                            .Units("px")
                            .Minimum(1)
                            .Value(BrushScale, BindingMode.TwoWay, bindingSource: this)
                            .Row(2),

                        new SliderEx()
                            .Label(L("Opacity"))
                            .Units("%")
                            .Value(BrushOpacity, BindingMode.TwoWay, bindingSource: this)
                            .Row(3),

                        new SliderEx()
                            .Label(L("Spacing"))
                            .Units("px")
                            .Value(BrushSpacing, BindingMode.TwoWay, bindingSource: this)
                            .Row(4),

                        new ToggleSwitch()
                            .IsChecked(DrawingState.IsPixelPerfectDrawingModeEnabled, BindingMode.TwoWay, bindingSource: DrawingState)
                            .Content(L("Pixel perfect mode"))
                            .Row(5)
                    ));

    public Pix2d.Primitives.Drawing.BrushSettings CurrentPixelBrushPreset
    {
        get => DrawingState.CurrentPixelBrushPreset;
        set
        {
            DrawingState.CurrentPixelBrushPreset = value;

            if (value != null)
            {
                DrawingState.CurrentBrushSettings = value.Clone();
                OnPropertyChanged();
                UpdateSliders();
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

    private void UpdateSliders()
    {
        OnPropertyChanged(nameof(BrushScale));
        OnPropertyChanged(nameof(BrushOpacity));
        OnPropertyChanged(nameof(BrushSpacing));
    }
}