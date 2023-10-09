using Pix2d.Shared;
using Pix2d.ViewModels.Layers;

namespace Pix2d.Views.Layers;

public class BackgroundSelectorView : ViewBaseSingletonVm<LayersListViewModel>
{
    protected override object Build(LayersListViewModel vm) =>
        new Grid()
            .Children(
                new Button()
                    .Classes("color-button")
                    .Width(42)
                    .Height(42)
                    .CornerRadius(32)
                    .BorderThickness(3)
                    .BorderBrush(Colors.White.ToBrush())
                    .Background(vm.ResultBackgroundBrush, 
                        bindingMode: BindingMode.OneWay, 
                        bindingSource: vm)
                    .Flyout(
                        new Flyout()
                            .Content(
                                new Grid()
                                    .Rows("Auto, Auto, Auto")
                                    .Children(
                                        new TextBlock().Text("Background"),
                                        new Pix2dColorPicker().Row(1)
                                            .Margin(10)
                                            .Color(@vm.SelectedBackgroundColor, BindingMode.TwoWay)
                                            .Margin(0, 8)
                                            .Width(200)
                                            .Height(140),
                                        new ToggleSwitch().Row(2)
                                            .IsChecked(vm.UseBackgroundColor, BindingMode.TwoWay, bindingSource: vm)
                                            .Content("Show background")
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
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .FontSize(18)
                    .Content("\xE7B3")
            );
}