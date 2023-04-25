using SkiaSharp;

namespace Pix2d.Views;

public class GridSettingsView : ComponentBase
{
    protected override object Build() =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Child(
                new StackPanel()
                    .Margin(8)
                    .Children(
                        new TextBlock()
                            .Text("Grid settings"),
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text("Cell size"),
                        new Grid()
                            .Cols("*,*,*")
                            .Children(
                                new NumericUpDown()
                                    .Value(GridCellSizeWidth, BindingMode.TwoWay, bindingSource: this),

                                new TextBlock().Col(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(8)
                                    .Text("✕"),

                                new NumericUpDown().Col(2)
                                    .Value(GridCellSizeHeight, BindingMode.TwoWay, bindingSource: this)
                            ),
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text("Show grid"),

                        new ToggleSwitch().Margin(0, 8, 0, 0)
                            .IsChecked(ShowGrid, BindingMode.TwoWay, bindingSource: this)
                    ) //stack panel childern
                );

    public bool ShowGrid
    {
        get => CoreServices.SnappingService.ShowGrid;
        set
        {
            CoreServices.SnappingService.ShowGrid = value;
            OnPropertyChanged();
        }
    }

    public int GridCellSizeWidth
    {
        get => (int)CoreServices.SnappingService.GridCellSize.Width;
        set
        {
            var oldSize = CoreServices.SnappingService.GridCellSize;
            CoreServices.SnappingService.GridCellSize = new SKSize(value, oldSize.Height);
            OnPropertyChanged();
        }
    }
    public int GridCellSizeHeight
    {
        get => (int)CoreServices.SnappingService.GridCellSize.Height;
        set
        {
            var oldSize = CoreServices.SnappingService.GridCellSize;
            CoreServices.SnappingService.GridCellSize = new SKSize(oldSize.Width, value);
            OnPropertyChanged();
        }
    }

}