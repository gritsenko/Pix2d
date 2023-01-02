using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public partial class ZoomPanelView : ViewBaseSingletonVm<ZoomBarViewModel>
{
    protected override object Build(ZoomBarViewModel vm) =>
        new Grid()
            .Height(32)
            .Cols("32,*,32")
            .Children(

                new Button().Col(0)
                    .Command(vm.ZoomOutCommand)
                    .With(ButtonStyle)
                    .Content("-"),

                new Button().Col(1)
                    .Command(vm.ZoomAllCommand)
                    .Content(@vm.CurrentPercentZoom)
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .VerticalAlignment(VerticalAlignment.Stretch)
                    .FontSize(12)
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .MinWidth(80),
                
                new Button().Col(2)
                    .With(ButtonStyle)
                    .Command(vm.ZoomInCommand)
                    .Content("+")
            );

    private static void ButtonStyle(Button b) => b
        .Padding(0)
        .HorizontalAlignment(HorizontalAlignment.Stretch)
        .VerticalAlignment(VerticalAlignment.Stretch)
        .Background(StaticResources.Brushes.SelectedItemBrush)
        .BorderBrush(StaticResources.Brushes.SelectedItemBrush);
}