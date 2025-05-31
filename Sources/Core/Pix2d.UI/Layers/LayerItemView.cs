using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Pix2d.Common.Extensions;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Layers;

public class LayerItemView(LayerItemViewModel viewModel) : ComponentBase<LayerItemViewModel>(viewModel)
{
    private static IControlTemplate buttonTemplate =
        new FuncControlTemplate<Button>((button, scope) => new ContentPresenter() { Content = button.Content, Background = StaticResources.Brushes.CheckerTilesBrush});

    protected override object Build(LayerItemViewModel vm)
    {
        vm.Invalidated += StateHasChanged;

        return new Border()
            .CornerRadius(6)
            .ClipToBounds(true)
            .Child(
                new Grid()
                    .Margin(0)
                    .Width(80)
                    .Height(80)
                    .Children(
                        new Button()
                            .Padding(0)
                            .OnClick(_ => LeftPointerPressed?.Invoke())
                            .Template(buttonTemplate)
                            .Content(
                                new Rectangle()
                                    .Width(100)
                                    .Height(100)
                                    .Fill(() => new ImageBrush(vm.Preview.ToBitmap()))
                            )
                            .OnPointerPressed(OnRightPointerPressed),
                        new Grid()
                            .Rows("*,*,*")
                            .Background("#66363D45".ToColor().ToBrush())
                            .Width(32)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Children(
                                new Button()
                                    .Row(0)
                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .FontSize(18)
                                    .OnClick(_ => UpdateState(vm.ToggleLayerVisibility))
                                    .Foreground(() => vm.SourceNode.IsVisible ? Brushes.White : Brushes.LightGray)
                                    .Content("\xe92a"),
                                new Button()
                                    .Row(1)
                                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                    .FontSize(18)
                                    .OnClick(_ =>
                                    {
                                        vm.SourceNode.LockTransparentPixels = !vm.SourceNode.LockTransparentPixels;
                                        StateHasChanged();
                                    })
                                    .Foreground(() =>
                                        vm.SourceNode.LockTransparentPixels ? Brushes.White : Brushes.LightGray)
                                    .Content("\xe901"),
                                new Button()
                                    .Row(3)
                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .FontSize(18)
                                    .IsVisible(() => vm.SourceNode.HasEffects)
                                    .OnClick(_ => UpdateState(()=>
                                    {
                                        vm.SourceNode.ShowEffects = !vm.SourceNode.ShowEffects;
                                        ViewPortRefreshService.Refresh();
                                    }))
                                    .Foreground(() => vm.SourceNode.ShowEffects ? Brushes.White : Brushes.LightGray)
                                    .Content("\xe939")
                            ), //buttons grid
                        new Grid()
                            .VerticalAlignment(VerticalAlignment.Bottom)
                            .HorizontalAlignment(HorizontalAlignment.Right)
                            .IsVisible(() => vm.SourceNode.BlendMode != SKBlendMode.SrcOver)
                            .Children(
                                new TextBlock().Text(() => vm.SourceNode.BlendMode.ToString()).Foreground(Brushes.White)
                                    .Margin(8),
                                new TextBlock().Text(() => vm.SourceNode.BlendMode.ToString()).Foreground(Brushes.Black)
                                    .Margin(7)
                            )
                    )
            );
    }

    [Inject] private IViewPortRefreshService ViewPortRefreshService { get; set; } = null!;

    public Action? LeftPointerPressed { get; set; } = null!;

    public Action? RightPointerPressed { get; set; } = null!;

    private void OnRightPointerPressed(PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
        {
            RightPointerPressed?.Invoke();
        }
    }
}