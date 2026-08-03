using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI;

public partial class GridSettingsView(AppState appState, ISettingsService settingsService)
    : ViewBase<GridSettingsView.State>(new State(appState, settingsService))
{
    protected override object Build(State state) =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Child(
                new StackPanel()
                    .Margin(8)
                    .Children(
                        new TextBlock()
                            .Text(L("Grid settings")),
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text(L("Cell size")),
                        new Grid()
                            .Cols("*,*,*")
                            .Children(
                                new NumericUpDown()
                                    .Value(state, x => x.GridCellSizeWidth, BindingMode.TwoWay),
                                new TextBlock().Col(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(8)
                                    .Text("✕"),
                                new NumericUpDown().Col(2)
                                    .Value(state, x => x.GridCellSizeHeight, BindingMode.TwoWay)
                            ),
                        new Grid()
                            .Margin(0, 12, 0, 0)
                            .Cols("*,Auto")
                            .Children(
                                new TextBlock()
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Text(L("Line color")),
                                new ColorPickerButton().Col(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Color(state, x => x.GridColor, BindingMode.TwoWay)
                            ),
                        new SliderEx()
                            .Margin(0, 8, 0, 0)
                            .Label(L("Opacity"))
                            .Units("%")
                            .Minimum(0)
                            .Maximum(100)
                            .Value(state, x => x.GridOpacity, BindingMode.TwoWay),
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text(L("Show grid")),
                        new ToggleSwitch().Margin(0, 8, 0, 0)
                            .IsChecked(state, x => x.ShowGrid, BindingMode.TwoWay)
                    )
                );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ISettingsService _settingsService;
        private readonly DispatcherTimer _persistTimer;

        // Guards the state->view sync so re-reading the current project's values doesn't
        // echo back into the ViewPortState (and stomp another project on a tab switch).
        private bool _syncing;

        [ObservableProperty]
        public partial bool? ShowGrid { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeWidth { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeHeight { get; set; }

        /// <summary>Opaque RGB of the grid lines; alpha is edited separately via <see cref="GridOpacity"/>.</summary>
        [ObservableProperty]
        public partial SKColor GridColor { get; set; }

        [ObservableProperty]
        public partial double GridOpacity { get; set; }

        // Always resolve the LIVE current project's viewport state — the flyout is built once with
        // MainView, so a captured instance goes stale after the first real project loads or a tab
        // switch (see the WatchFor gotcha in CLAUDE.md), and toggles would write to an orphan state
        // that nothing renders from.
        private ViewPortState ViewPortState => _appState.CurrentProject.ViewPortState;

        public State(AppState appState, ISettingsService settingsService)
        {
            _appState = appState;
            _settingsService = settingsService;
            _persistTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _persistTimer.Tick += (_, _) => PersistGridColor();

            SyncFromState();

            // Re-bind on project switch and reflect external changes (e.g. the Toggle grid shortcut).
            appState.WatchForCurrentProjectViewPort(x => x.ShowGrid, SyncFromState);
            appState.WatchForCurrentProjectViewPort(x => x.GridSpacing, SyncFromState);
            // Line color/opacity are app-wide (#223), so they hang off AppState rather than the viewport.
            appState.WatchFor(x => x.GridColor, SyncFromState);
        }

        private void SyncFromState()
        {
            _syncing = true;
            ShowGrid = ViewPortState.ShowGrid;
            GridCellSizeWidth = (decimal)ViewPortState.GridSpacing.Width;
            GridCellSizeHeight = (decimal)ViewPortState.GridSpacing.Height;

            var color = _appState.GridColor;
            GridColor = color.WithAlpha(255);
            GridOpacity = Math.Round(color.Alpha / 255d * 100d);
            _syncing = false;
        }

        partial void OnShowGridChanged(bool? value)
        {
            if (_syncing) return;
            ViewPortState.ShowGrid = value ?? false;
        }

        partial void OnGridCellSizeWidthChanged(decimal? value)
        {
            if (_syncing) return;
            var oldSize = ViewPortState.GridSpacing;
            ViewPortState.GridSpacing = new SKSize((float)(value ?? 1), oldSize.Height);
        }

        partial void OnGridCellSizeHeightChanged(decimal? value)
        {
            if (_syncing) return;
            var oldSize = ViewPortState.GridSpacing;
            ViewPortState.GridSpacing = new SKSize(oldSize.Width, (float)(value ?? 1));
        }

        partial void OnGridColorChanged(SKColor value) => ApplyGridColor();

        partial void OnGridOpacityChanged(double value) => ApplyGridColor();

        private void ApplyGridColor()
        {
            if (_syncing) return;

            var alpha = (byte)Math.Clamp(Math.Round(GridOpacity / 100d * 255d), 0, 255);
            var color = GridColor.WithAlpha(alpha);
            if (_appState.GridColor == color) return;

            // State first: SnappingService pushes it to the scene and repaints, so dragging the opacity
            // slider or the color picker previews live on the canvas.
            _appState.GridColor = color;

            // Persisting is debounced because SettingsService.Set re-serializes the ENTIRE settings file
            // (user brush presets embed base64 PNGs), and a scrub raises this on every pointer tick.
            _persistTimer.Stop();
            _persistTimer.Start();
        }

        private void PersistGridColor()
        {
            _persistTimer.Stop();
            _settingsService.Set(nameof(AppState.GridColor), GridDefaults.FormatColor(_appState.GridColor));
        }
    }
}
