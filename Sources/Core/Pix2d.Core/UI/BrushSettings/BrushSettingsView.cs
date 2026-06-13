using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using System.Collections.ObjectModel;
using Colors = Avalonia.Media.Colors;

namespace Pix2d.UI.BrushSettings;

public partial class BrushSettingsView(AppState appState) : ViewBase<BrushSettingsView.State>(new State(appState))
{
    protected override StyleGroup? BuildStyles() => [
        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>())
            .Background(StaticResources.Brushes.BrushItemBrush)
            .Margin(2)
            .Width(44)
            .Height(44)
            .CornerRadius(12)
    ];

    protected override object Build(State state) =>
        new ScrollViewer()
            .Content(
                new Grid()
                    .Rows("Auto,*,64,64,64,Auto")
                    .Margin(8, 0)
                    .Children(
                        new TextBlock()
                            .Classes("body11")
                            .Padding(4, 12, 0, 4)
                            .Text(L("Presets").ToUpperInvariant()),

                        new ListBox()
                            .Background(Avalonia.Media.Brushes.Transparent)
                            .ScrollViewer_HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Row(1)
                            .Padding(0)
                            .MinHeight(72)
                            .BorderThickness(0)
                            .Padding(0)
                            .ItemsSource(state.BrushPresets)
                            .SelectedItem(state, x => x.CurrentPixelBrushPreset, BindingMode.TwoWay)
                            .ItemsPanel(StaticResources.Templates.WrapPanelTemplate)
                            .ItemTemplate((Primitives.Drawing.BrushSettings itemVm) =>
                                ViewFactory.Create<BrushItemView>()
                                            .Preset(itemVm)
                                            .ShowSizeText(true)
                                    ),

                        new Grid()
                            .Cols("*,Auto")
                            .Row(2)
                            .Children(
                                new SliderEx()
                                    .Label(L("Size"))
                                    .Units("px")
                                    .Minimum(1)
                                    .Value(state, x => x.BrushScale, BindingMode.TwoWay),
                                BuildPressureToggle()
                                    .Col(1)
                                    .IsChecked(state, x => x.IsSizePressureEnabled, BindingMode.TwoWay)),

                        new Grid()
                            .Cols("*,Auto")
                            .Row(3)
                            .Children(
                                new SliderEx()
                                    .Label(L("Opacity"))
                                    .Units("%")
                                    .Value(state, x => x.BrushOpacity, BindingMode.TwoWay),
                                BuildPressureToggle()
                                    .Col(1)
                                    .IsChecked(state, x => x.IsOpacityPressureEnabled, BindingMode.TwoWay)),

                        new SliderEx()
                            .Label(L("Spacing"))
                            .Units("px")
                            .Value(state, x => x.BrushSpacing, BindingMode.TwoWay)
                            .Row(4),

                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsPixelPerfectDrawingModeEnabled, BindingMode.TwoWay)
                            .FontSize(9)
                            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
                            .Content(L("Pixel perfect mode").ToUpperInvariant())
                            .Row(5)
                    ));

    // Compact toggle placed next to the Size / Opacity sliders. When on, stylus pen pressure scales that
    // property while drawing. The pen glyph (Segoe MDL2) reads as "responds to the stylus".
    private static ToggleButton BuildPressureToggle() =>
        new ToggleButton()
            .ToolTip_Tip(L("Pressure sensitivity"))
            .VerticalAlignment(VerticalAlignment.Center)
            .Margin(8, 0, 0, 0)
            .Padding(0)
            .Width(36)
            .Height(36)
            .Content(
                new TextBlock()
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(16)
                    .Text("")
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center));

    public sealed partial class State : ObservableObject
    {
        private readonly SpriteEditorState _drawingState;
        private bool _isSyncing;

        [ObservableProperty]
        public partial List<Pix2d.Primitives.Drawing.BrushSettings> BrushPresets { get; set; } = [];

        [ObservableProperty]
        public partial Pix2d.Primitives.Drawing.BrushSettings? CurrentPixelBrushPreset { get; set; }

        [ObservableProperty]
        public partial double BrushScale { get; set; }

        [ObservableProperty]
        public partial double BrushOpacity { get; set; }

        [ObservableProperty]
        public partial double BrushSpacing { get; set; }

        [ObservableProperty]
        public partial bool IsPixelPerfectDrawingModeEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsSizePressureEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsOpacityPressureEnabled { get; set; }

        public State(AppState appState)
        {
            _drawingState = appState.SpriteEditorState;

            SyncFromDrawingState();

            _drawingState.WatchFor(x => x.BrushPresets, () => BrushPresets = _drawingState.BrushPresets);
            _drawingState.WatchFor(x => x.IsPixelPerfectDrawingModeEnabled,
                () => IsPixelPerfectDrawingModeEnabled = _drawingState.IsPixelPerfectDrawingModeEnabled);
            _drawingState.WatchFor(x => x.CurrentBrushSettings, SyncFromDrawingState);
            _drawingState.WatchFor(x => x.CurrentPixelBrushPreset, SyncFromDrawingState);
        }

        partial void OnCurrentPixelBrushPresetChanged(Pix2d.Primitives.Drawing.BrushSettings? value)
        {
            if (_isSyncing || value?.Brush == null)
                return;

            _drawingState.CurrentPixelBrushPreset = value;
            _drawingState.CurrentBrushSettings = value.Clone();
            SyncFromDrawingState();
        }

        partial void OnBrushScaleChanged(double value)
        {
            if (_isSyncing)
                return;

            UpdateBrush(brush => brush.Scale = (float)value);
        }

        partial void OnBrushOpacityChanged(double value)
        {
            if (_isSyncing)
                return;

            UpdateBrush(brush => brush.Opacity = (float)value / 100f);
        }

        partial void OnBrushSpacingChanged(double value)
        {
            if (_isSyncing)
                return;

            UpdateBrush(brush => brush.Spacing = (float)value);
        }

        partial void OnIsPixelPerfectDrawingModeEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _drawingState.IsPixelPerfectDrawingModeEnabled = value;
        }

        partial void OnIsSizePressureEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            UpdateBrush(brush => brush.PressureAffectsSize = value);
        }

        partial void OnIsOpacityPressureEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            UpdateBrush(brush => brush.PressureAffectsOpacity = value);
        }

        private void SyncFromDrawingState()
        {
            _isSyncing = true;

            BrushPresets = _drawingState.BrushPresets;
            CurrentPixelBrushPreset = _drawingState.CurrentPixelBrushPreset;
            BrushScale = _drawingState.CurrentBrushSettings.Scale;
            BrushOpacity = _drawingState.CurrentBrushSettings.Opacity * 100d;
            BrushSpacing = _drawingState.CurrentBrushSettings.Spacing;
            IsSizePressureEnabled = _drawingState.CurrentBrushSettings.PressureAffectsSize;
            IsOpacityPressureEnabled = _drawingState.CurrentBrushSettings.PressureAffectsOpacity;
            IsPixelPerfectDrawingModeEnabled = _drawingState.IsPixelPerfectDrawingModeEnabled;

            _isSyncing = false;
        }

        private void UpdateBrush(Action<Pix2d.Primitives.Drawing.BrushSettings> update)
        {
            var brush = _drawingState.CurrentBrushSettings.Clone();
            update(brush);

            if (brush.Equals(_drawingState.CurrentBrushSettings))
                return;

            _drawingState.CurrentBrushSettings = brush;
        }
    }
}
