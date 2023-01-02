using Pix2d.Resources;
using Pix2d.Shared;
using Pix2d.ViewModels.Layers;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views.Layers;

public class BackgroundSelectorView : ViewBaseSingletonVm<LayersListViewModel>
{
    protected override object Build(LayersListViewModel vm) =>
        new Grid()
            .Children(
                new Button()
                    .Padding(10)
                    .Background(StaticResources.Brushes.CheckerTilesBrushNoScale)
                    .Width(42)
                    .Height(42)
                    .CornerRadius(32)
                    .Content(
                        new Ellipse()
                            .Width(42)
                            .Height(42)
                            .StrokeThickness(3)
                            .Stroke(Brushes.White)
                            .Fill(@vm.ResultBackgroundColor, StaticResources.Converters.SKColorToBrushConverter)
                    )
                    .Flyout(
                        new Flyout()
                            .Content(
                                new Grid().Width(200).Height(200)
                                    .Rows("Auto,*")
                                    .Children(
                                        new TextBlock().Text("Background"),
                                        new ColorPicker().Row(1)
                                            .Margin(10)
                                            .Color(@vm.SelectedBackgroundColor, BindingMode.TwoWay)
                                            .Margin(0, 8)
                                            .Height(140)
                                            
                                    )
                            )
                    ),
                new Button()
                    .Command(vm.ToggleUseBackgroundCommand)
                    .Foreground(@vm.UseBackgroundColor, StaticResources.Converters.BoolToBrushButtonForegroundConverter)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Margin(1)
                    .Padding(0)
                    .Width(32)
                    .Height(32)
                    .BorderThickness(0)
                    .Background(Brushes.Transparent)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(18)
                    .Content("\xE7B3")
            );
}