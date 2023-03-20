using Pix2d.ViewModels;

namespace Pix2d.Views;

public class GridSettingsView : ViewBaseSingletonVm<MainViewModel>
{
    protected override object Build(MainViewModel vm) =>
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
                                    .Value(@vm.GridCellSizeWidth, BindingMode.TwoWay),

                                new TextBlock().Col(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(8)
                                    .Text("✕"),

                                new NumericUpDown().Col(2)
                                    .Value(@vm.GridCellSizeHeight, BindingMode.TwoWay)
                            ),
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text("Show grid"),

                        new ToggleSwitch().Margin(0, 8, 0, 0)
                            .IsChecked(@vm.ShowGrid, BindingMode.TwoWay)
                    ) //stack panel childern
                );

}