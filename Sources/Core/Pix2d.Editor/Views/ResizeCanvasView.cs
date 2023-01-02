using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public class ResizeCanvasView : ViewBaseSingletonVm<ResizeCanvasSizeViewModel>
{
    protected override object Build(ResizeCanvasSizeViewModel vm) =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Child(
                new StackPanel()
                    .Margin(8)
                    .Children(
                        new Grid()
                            .Cols("*,*,*")
                            .Rows("20,*")
                            .Children(
                                new TextBlock()
                                    .Text("Width"),

                                new NumericUpDown()
                                    .Row(1)
                                    .Value(@vm.Width, BindingMode.TwoWay),

                                new TextBlock().Col(1)
                                    .Row(1)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Text("✕"),

                                new TextBlock()
                                    .Col(2)
                                    .Text("Height"),
                                new NumericUpDown().Col(2)
                                    .Row(1)
                                    .Value(@vm.Height, BindingMode.TwoWay)
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Horizontal anchor"),

                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(@vm.HorizontalAnchor)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content("Left"),
                                new ComboBoxItem().Content("Center"),
                                new ComboBoxItem().Content("Right")
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Vertical anchor"),

                        new ComboBox()
                            .Margin(0, 8, 0, 0)
                            .SelectedIndex(@vm.HorizontalAnchor)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Items(
                                new ComboBoxItem().Content("Top"),
                                new ComboBoxItem().Content("Center"),
                                new ComboBoxItem().Content("Bottom")
                            ),

                        new TextBlock()
                            .Margin(0, 16, 0, 0)
                            .Text("Keep aspect ratio"),

                        new ToggleSwitch().Margin(0, 0, 0, 0)
                            .IsChecked(@vm.KeepAspect, BindingMode.TwoWay),

                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Spacing(8)
                            .Children(
                                new Button()
                                    .Content("Apply")
                                    .Background(Brushes.Gray)
                                    .Width(80)
                                    .Height(30)
                                    .Command(vm.ResizeCanvasCommand),
                                new Button()
                                    .Content("Reset")
                                    .Background(Brushes.Gray)
                                    .Width(80)
                                    .Height(30)
                                    .Command(vm.ResetCommand)
                                )
                    )
            );
    public void UpdateData()
    {
        var vm = ViewModel;
        vm.UpdateSizeProperties();

    }
}