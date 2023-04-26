using Pix2d.Shared;
using Pix2d.ViewModels;
using Pix2d.ViewModels.ToolBar.ToolSettings;
using Pix2d.ViewModels.ToolSettings;
using static Pix2d.Resources.StaticResources;
using Colors = Avalonia.Media.Colors;

namespace Pix2d.Views;

public class BrushSettingsView : ViewBaseSingletonVm<BrushToolSettingsViewModel>
{
    public static IDataTemplate BrushPreviewTemplate { get; set; } = new FuncDataTemplate<BrushPresetViewModel>(
        (itemVm, ns) =>
            new Grid()
                {
                    DataContext = itemVm.Preview
                }
                .Background(new Binding("Bitmap") { Converter = Converters.SKBitmapToBrushConverter })
                .Width(48)
                .Height(48)
    );

    protected override object Build(BrushToolSettingsViewModel vm) =>
        new ScrollViewer()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Content(
                new Grid()
                    .Rows("Auto,*,64,64,64,Auto")
                    .Margin(8, 0)
                    .Children(
                        new TextBlock()
                            .Padding(4, 12, 0, 4)
                            .FontSize(12)
                            .Foreground(Colors.White.ToBrush())
                            .Text("Presets"),

                        new ListBox()
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Row(1)
                            .Padding(0)
                            .MinHeight(72)
                            .BorderThickness(1)
                            .Padding(4)
                            .Items(Bind(vm.BrushPresets))
                            .SelectedItem(Bind(vm.CurrentPixelBrushPreset, BindingMode.TwoWay))
                            .ItemsPanel(Templates.WrapPanelTemplate)
                            .ItemTemplate(BrushPreviewTemplate),

                        new SliderEx()
                            .Header("Size")
                            .Units("px")
                            .Minimum(1)
                            .Value(@vm.BrushScale, BindingMode.TwoWay)
                            .Row(2),

                        new SliderEx()
                            .Header("Opacity")
                            .Units("%")
                            .Value(@vm.BrushOpacity, BindingMode.TwoWay)
                            .Row(3),

                        new SliderEx()
                            .Header("Spacing")
                            .Units("px")
                            .Value(@vm.BrushSpacing, BindingMode.TwoWay)
                            .Row(4),

                        new ToggleSwitch()
                            .IsChecked(@vm.IsPixelPerfectModeEnabled, BindingMode.TwoWay)
                            .Row(5)
                    ));
}