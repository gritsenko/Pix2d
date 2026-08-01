using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Plugins.Drawing.Brushes;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;
using System.Collections.ObjectModel;
using Colors = Avalonia.Media.Colors;

namespace Pix2d.UI.BrushSettings;

public partial class BrushSettingsView(AppState appState, IDrawingService drawingService, IDialogService dialogService)
    : ViewBase<BrushSettingsView.State>(new State(appState, drawingService, dialogService))
{
    protected override StyleGroup? BuildStyles() => [
        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>())
            .Background(StaticResources.Brushes.BrushItemBrush)
            .Margin(2)
            .Width(44)
            .Height(44)
            .CornerRadius(12)
    ];

    // 100% preview box height; tall enough to show a swoosh for the largest presets without dominating the panel.
    private const int PreviewHeight = 76;

    protected override object Build(State state) =>
        new ScrollViewer()
            .Content(
                new Grid()
                    // Slider rows are Auto so each SliderEx uses its own (compact) height instead of being
                    // stretched to a fixed row — keep them in sync with SliderEx's intrinsic size.
                    .Rows("Auto,Auto,Auto,Auto,Auto,Auto,Auto")
                    .Margin(8, 0)
                    .Children(
                        // Live stroke preview at 100% scale. Reflects size / opacity / spacing and the stylus
                        // pressure toggles (thin→thick→thin), like the stroke preview in other editors.
                        new Border()
                            .Row(0)
                            .Height(PreviewHeight)
                            .Margin(0, 12, 0, 4)
                            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
                            .ClipToBounds(true)
                            // The transparency checker is rendered into the preview bitmap itself, so this box
                            // only needs a subtle frame around it.
                            .BorderBrush(StaticResources.Brushes.PanelStrokeBrush)
                            .BorderThickness(1)
                            .OnSizeChanged(e => state.SetPreviewSize(e.NewSize))
                            .Child(
                                new Panel().Children(
                                    new Image()
                                        .Stretch(Stretch.Fill)
                                        .Source(state, x => x.StrokePreview),

                                    // Round corner button: cycles the preview backdrop (dark checker → white →
                                    // light checker) so the stroke can be judged against different surfaces.
                                    new Button()
                                        .Width(24)
                                        .Height(24)
                                        .MinWidth(0)
                                        .MinHeight(0)
                                        .Padding(0)
                                        .CornerRadius(12)
                                        .HorizontalAlignment(HorizontalAlignment.Right)
                                        .VerticalAlignment(VerticalAlignment.Top)
                                        .Margin(0, 6, 6, 0)
                                        .Background(new SolidColorBrush(Color.FromArgb(150, 20, 20, 20)))
                                        .BorderBrush(StaticResources.Brushes.PanelStrokeBrush)
                                        .BorderThickness(1)
                                        .ToolTip_Tip(L("Preview background"))
                                        .OnClick(_ => state.CyclePreviewBackground())
                                        .Content(new PathIcon()
                                            .Width(13)
                                            .Height(13)
                                            .Foreground(Avalonia.Media.Brushes.White)
                                            .Data(StaticResources.Icons.GridIcon)))),

                        // Caption row doubles as the preset-library menu host, so the actions cost no extra
                        // height in an already-dense popup.
                        new Grid()
                            .Cols("*,Auto")
                            .Row(1)
                            .Children(
                                new TextBlock()
                                    .Classes("body11")
                                    .Padding(4, 12, 0, 4)
                                    .Text(L("Presets").ToUpperInvariant()),
                                BuildPresetMenuButton(state).Col(1)),

                        new ListBox()
                            .Background(Avalonia.Media.Brushes.Transparent)
                            .ScrollViewer_HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .ScrollViewer_VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Row(2)
                            .Padding(0)
                            .MinHeight(72)
                            // Cap to two rows of 44px items (+2px margins); a third row scrolls within the list.
                            .MaxHeight(96)
                            .BorderThickness(0)
                            // Bound, not a plain value: the presets list is REPLACED (not mutated) whenever a
                            // user preset is saved or deleted, and a static ItemsSource would keep showing the
                            // list instance captured when the view was first built.
                            .ItemsSource(state, x => x.BrushPresets)
                            .SelectedItem(state, x => x.CurrentPixelBrushPreset, BindingMode.TwoWay)
                            .ItemsPanel(StaticResources.Templates.WrapPanelTemplate)
                            .ItemTemplate((Primitives.Drawing.BrushSettings itemVm) =>
                                ViewFactory.Create<BrushItemView>()
                                            .Preset(itemVm)
                                            .ShowSizeText(true)
                                    ),

                        new Grid()
                            .Cols("*,Auto")
                            .Row(3)
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
                            .Row(4)
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
                            .Row(5),

                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsPixelPerfectDrawingModeEnabled, BindingMode.TwoWay)
                            .FontSize(9)
                            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
                            .Content(L("Pixel perfect mode").ToUpperInvariant())
                            .Row(6)
                    ));

    /// <summary>
    /// The preset-library actions, behind one compact <c>⋯</c> button — the same pattern the palette library
    /// uses in <c>ColorPickerView</c>, and for the same reason: the popup has no room for a button row.
    ///
    /// <para>Loading a preset is tapping its tile, so there is nothing to name and nothing to browse — the
    /// preset row already *is* the library. Delete now acts on ANY selected preset: a user preset is dropped
    /// for good, a built-in one is only hidden (and comes back via Reset). The two "save selection" items
    /// capture the current pixel selection as a new stamp preset — colors keeps the selection's own pixels,
    /// mask/recolorable treats it as a shape tinted by the paint color like every other brush.</para>
    /// </summary>
    private static Control BuildPresetMenuButton(State state) =>
        new Button()
            .HorizontalAlignment(HorizontalAlignment.Right)
            .VerticalAlignment(VerticalAlignment.Center)
            .Margin(0, 8, 0, 0)
            .Width(28)
            .Height(28)
            .Padding(0)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.ButtonBackgroundBrush)
            .ToolTip_Tip(L("Brush presets"))
            .Content(new TextBlock()
                .Text("⋯")
                .FontSize(18)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .VerticalAlignment(VerticalAlignment.Center)
                .Foreground(StaticResources.Brushes.IconForegroundBrush))
            .Flyout(
                new MenuFlyout()
                    .Placement(PlacementMode.Bottom)
                    .ItemsSource(new Control[]
                    {
                        new MenuItem().Header(L("Save current brush as preset"))
                            .OnClick(_ => state.SaveCurrentBrushAsPreset()),
                        new Separator(),
                        new MenuItem().Header(L("Save selection as brush (colors)"))
                            .IsEnabled(state, x => x.HasSelection)
                            .OnClick(_ => state.SaveSelectionAsBrushPreset(useOriginalColors: true)),
                        new MenuItem().Header(L("Save selection as brush (recolorable)"))
                            .IsEnabled(state, x => x.HasSelection)
                            .OnClick(_ => state.SaveSelectionAsBrushPreset(useOriginalColors: false)),
                        new Separator(),
                        new MenuItem().Header(L("Delete selected preset"))
                            .IsEnabled(state, x => x.CanDeleteSelectedPreset)
                            .OnClick(e => _ = state.DeleteSelectedPresetAsync()),
                        new MenuItem().Header(L("Reset presets to defaults"))
                            .OnClick(e => _ = state.ResetPresetsToDefaultsAsync()),
                    }));

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
        private readonly IDrawingService _drawingService;
        private readonly IDialogService _dialogService;
        private bool _isSyncing;

        // Preview render size, taken from the preview box's actual measured bounds.
        private int _previewWidth;
        private int _previewHeight;

        // Backdrop the sample stroke is drawn over; cycled by the preview's corner button.
        private BrushPreviewBackground _previewBackground = BrushPreviewBackground.DarkChecker;

        [ObservableProperty]
        public partial IImage? StrokePreview { get; set; }

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

        /// <summary>Gates the Delete menu item: nothing is selected to delete otherwise.</summary>
        [ObservableProperty]
        public partial bool CanDeleteSelectedPreset { get; set; }

        /// <summary>Gates the two "save selection as brush" menu items. The popup outlives any one tool
        /// activation, so this is kept live via <see cref="IDrawingLayer"/>'s selection events rather than
        /// read once at construction.</summary>
        [ObservableProperty]
        public partial bool HasSelection { get; set; }

        public State(AppState appState, IDrawingService drawingService, IDialogService dialogService)
        {
            _drawingState = appState.SpriteEditorState;
            _drawingService = drawingService;
            _dialogService = dialogService;

            SyncFromDrawingState();

            _drawingState.WatchFor(x => x.BrushPresets, () => BrushPresets = _drawingState.BrushPresets);
            _drawingState.WatchFor(x => x.IsPixelPerfectDrawingModeEnabled,
                () => IsPixelPerfectDrawingModeEnabled = _drawingState.IsPixelPerfectDrawingModeEnabled);
            _drawingState.WatchFor(x => x.CurrentBrushSettings, SyncFromDrawingState);
            _drawingState.WatchFor(x => x.CurrentPixelBrushPreset, SyncFromDrawingState);
            _drawingState.WatchFor(x => x.CurrentColor, UpdateStrokePreview);

            // This view is created once and toggled via PopupView.IsOpen (never re-created), and
            // IDrawingService keeps one drawing layer for the app's lifetime (retargeted across project tabs,
            // not replaced) — so a single subscription here needs no matching Unsubscribe.
            HasSelection = _drawingService.DrawingLayer.HasSelection;
            _drawingService.DrawingLayer.PixelsSelected += (_, _) => HasSelection = _drawingService.DrawingLayer.HasSelection;
            _drawingService.DrawingLayer.SelectionRemoved += (_, _) => HasSelection = _drawingService.DrawingLayer.HasSelection;
        }

        /// <summary>
        /// Stores the live brush settings as a new preset tile and selects it, so the tile the user just
        /// created is the one highlighted (and therefore the one Delete would act on). Saving settings that
        /// already exist as a preset selects that preset instead of adding a duplicate.
        /// </summary>
        public void SaveCurrentBrushAsPreset()
        {
            var preset = _drawingService.SaveCurrentBrushAsPreset();
            if (preset == null)
                return;

            _drawingState.CurrentPixelBrushPreset = preset;
            SyncFromDrawingState();
        }

        /// <summary>
        /// Captures the current pixel selection as a new preset and selects it. No-op (and the menu item is
        /// disabled) when there is no active selection.
        /// </summary>
        public void SaveSelectionAsBrushPreset(bool useOriginalColors)
        {
            var preset = _drawingService.CreateBrushPresetFromSelection(useOriginalColors);
            if (preset == null)
                return;

            _drawingState.CurrentPixelBrushPreset = preset;
            SyncFromDrawingState();
        }

        /// <summary>
        /// Deletes the selected preset after a confirmation. A user preset is gone for good; a built-in one is
        /// only hidden, which the confirmation wording makes clear since "Reset to defaults" brings it back.
        /// </summary>
        public async Task DeleteSelectedPresetAsync()
        {
            var preset = _drawingState.CurrentPixelBrushPreset;
            if (preset == null)
                return;

            var message = preset.IsUserPreset
                ? L("Delete this brush preset?")
                : L("Remove this built-in preset? You can restore it later via Reset presets to defaults.");

            if (!await _dialogService.ShowYesNoDialog(message, L("Delete preset"), L("Delete"), L("Cancel")))
                return;

            _drawingService.DeleteBrushPreset(preset);
            SyncFromDrawingState();
        }

        /// <summary>Restores any built-in presets the user has removed. Saved presets are never touched.</summary>
        public async Task ResetPresetsToDefaultsAsync()
        {
            if (!await _dialogService.ShowYesNoDialog(
                    L("This restores any built-in presets you removed. Your saved presets are kept."),
                    L("Reset presets to defaults"), L("Reset"), L("Cancel")))
                return;

            _drawingService.ResetBrushPresetsToDefaults();
            SyncFromDrawingState();
        }

        /// <summary>Tracks the preview box's measured size so the sample stroke renders at 100% (1:1) scale.</summary>
        public void SetPreviewSize(Size size)
        {
            var w = (int)Math.Round(size.Width);
            var h = (int)Math.Round(size.Height);
            if (w <= 0 || h <= 0 || (w == _previewWidth && h == _previewHeight))
                return;

            _previewWidth = w;
            _previewHeight = h;
            UpdateStrokePreview();
        }

        /// <summary>Cycles the preview backdrop: dark checker → white → light checker → dark checker.</summary>
        public void CyclePreviewBackground()
        {
            _previewBackground = _previewBackground switch
            {
                BrushPreviewBackground.DarkChecker => BrushPreviewBackground.White,
                BrushPreviewBackground.White => BrushPreviewBackground.LightChecker,
                _ => BrushPreviewBackground.DarkChecker,
            };
            UpdateStrokePreview();
        }

        private void UpdateStrokePreview()
        {
            if (_previewWidth <= 0 || _previewHeight <= 0)
                return;

            var settings = _drawingState.CurrentBrushSettings;
            if (settings.Brush is not BasePixelBrush liveBrush)
                return;

            // Render on a throwaway brush: RenderStrokePreview mutates pressure + stamp cache, and preset
            // brushes are the same singletons the canvas draws with. The brush makes its own copy — an image
            // stamp has to carry its captured bitmap across, which reflecting on the type cannot do. Disposed
            // after use so its stamp bitmap isn't left for the finalizer.
            using var previewBrush = liveBrush.CreatePreviewInstance();

            previewBrush.PressureAffectsSize = settings.PressureAffectsSize;
            previewBrush.PressureAffectsOpacity = settings.PressureAffectsOpacity;
            _ = previewBrush.InitBrush(settings.Scale, settings.Opacity, settings.Spacing);

            // ToBitmap copies the pixels (Avalonia's Skia backend snapshots via SKImage.FromPixelCopy),
            // so the source SKBitmap can be released right away.
            using var source = previewBrush.RenderStrokePreview(_previewWidth, _previewHeight, _drawingState.CurrentColor, _previewBackground);
            StrokePreview = source.ToBitmap();
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
            CanDeleteSelectedPreset = _drawingState.CurrentPixelBrushPreset != null;

            _isSyncing = false;

            UpdateStrokePreview();
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
