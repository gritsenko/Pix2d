using Avalonia.Controls.Presenters;
using Pix2d.Resources;
using Pix2d.Shared;
using Pix2d.ViewModels.Layers;

namespace Pix2d.Views.Layers;

public class LayerItemView : ViewBase<LayerViewModel>
{
    public LayerItemView(LayerViewModel viewModel) : base(viewModel)
    {
    }

    private static IControlTemplate buttonTemplate =
        new FuncControlTemplate<Button>((button, scope) => new ContentPresenter() { Content = button.Content });

    public static FuncValueConverter<bool, IBrush> BoolToBrushButtonForegroundConverter = new(v => v ? Avalonia.Media.Brushes.White : Avalonia.Media.Brushes.LightGray);

    protected override object Build(LayerViewModel vm) =>
        new Grid()
            .Margin(2)
            .Width(80)
            .Height(80)
            .Children(
                new Button()
                    .Padding(0)
                    .Command(vm.SelectLayerCommand)
                    .CommandParameter(vm)
                    .Background(Brushes.Transparent)
                    .Template(buttonTemplate)
                    .Content(
                        new SKImageView()
                            .ShowCheckerBackground(true)
                            //.Width(52)
                            //.Height(52)
                            .Source(@vm.Preview)
                    ),

                new Grid()
                    .Rows("*,*,*")
                    .Background("#66363D45".ToColor().ToBrush())
                    .Width(32)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        new Button()
                            .Row(0)
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .FontSize(18)
                            .Command(@vm.ToggleVisibilityCommand)
                            .Foreground(@vm.IsVisible, BoolToBrushButtonForegroundConverter)
                            .Content("\xE7B3"),

                        new Button()
                            .Row(1)
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(18)
                            .Command(@vm.ToggleColorLockedCommand)
                            .Foreground(@vm.ColorLocked, BoolToBrushButtonForegroundConverter)
                            .Content("\xe901"),

                        new Button()
                            .Row(3)
                            .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                            .FontSize(18)
                            .IsVisible(@vm.HasEffects)
                            .Command(@vm.ToggleEffectsCommand)
                            .Foreground(@vm.ShowEffects, BoolToBrushButtonForegroundConverter)
                            .Content("\xe912")

                    ), //buttons grid

                new Grid()
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .IsVisible(@vm.ShowBlendModeName)
                    .Children(
                        new TextBlock().Text(vm.BlendModeStr).Foreground(Brushes.White).Margin(8),
                        new TextBlock().Text(vm.BlendModeStr).Foreground(Brushes.Black).Margin(7)
                    )
            );
}