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
        private readonly ViewPortState _viewPortState;

        [ObservableProperty]
        public partial bool? ShowGrid { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeWidth { get; set; }

        [ObservableProperty]
        public partial decimal? GridCellSizeHeight { get; set; }

        public State(AppState appState)
        {
            _viewPortState = appState.CurrentProject.ViewPortState;

            ShowGrid = _viewPortState.ShowGrid;
            GridCellSizeWidth = (decimal)_viewPortState.GridSpacing.Width;
            GridCellSizeHeight = (decimal)_viewPortState.GridSpacing.Height;
        }

        partial void OnShowGridChanged(bool? value)
        {
            _viewPortState.ShowGrid = value ?? false;
        }

        partial void OnGridCellSizeWidthChanged(decimal? value)
        {
            var oldSize = _viewPortState.GridSpacing;
            _viewPortState.GridSpacing = new SKSize((float)(value ?? 1), oldSize.Height);
        }

        partial void OnGridCellSizeHeightChanged(decimal? value)
        {
            var oldSize = _viewPortState.GridSpacing;
            _viewPortState.GridSpacing = new SKSize(oldSize.Width, (float)(value ?? 1));
        }
    }
}