using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI;

public partial class GridSettingsView(AppState appState) : ViewBase<GridSettingsView.State>(new State(appState))
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

        // Guards the state->view sync so re-reading the current project's values doesn't
        // echo back into the ViewPortState (and stomp another project on a tab switch).
        private bool _syncing;

        [ObservableProperty]
        public partial bool? ShowGrid { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeWidth { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeHeight { get; set; }

        // Always resolve the LIVE current project's viewport state — the flyout is built once with
        // MainView, so a captured instance goes stale after the first real project loads or a tab
        // switch (see the WatchFor gotcha in CLAUDE.md), and toggles would write to an orphan state
        // that nothing renders from.
        private ViewPortState ViewPortState => _appState.CurrentProject.ViewPortState;

        public State(AppState appState)
        {
            _appState = appState;

            SyncFromState();

            // Re-bind on project switch and reflect external changes (e.g. the Toggle grid shortcut).
            appState.WatchForCurrentProjectViewPort(x => x.ShowGrid, SyncFromState);
            appState.WatchForCurrentProjectViewPort(x => x.GridSpacing, SyncFromState);
        }

        private void SyncFromState()
        {
            _syncing = true;
            ShowGrid = ViewPortState.ShowGrid;
            GridCellSizeWidth = (decimal)ViewPortState.GridSpacing.Width;
            GridCellSizeHeight = (decimal)ViewPortState.GridSpacing.Height;
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
    }
}