using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public class InfoPanelView : ViewBaseSingletonVm<InfoPanelViewModel>
{
    protected override object Build(InfoPanelViewModel vm) =>
        new Grid()
            .Cols("28,70,24,*")
            .IsHitTestVisible(false)
            //.Background("#7F444E59".ToColor().ToBrush())
            .Height(28)
            .Children(
                new TextBlock()
                    .Text("\xE902")
                    .Padding(8, 0, 0, 0)
                    .With(LabelStyle),

                new TextBlock().Col(1)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Text(@vm.PointerInfo),

                new TextBlock().Col(2)
                    .Text("\xE921")
                    .With(LabelStyle),

                new TextBlock().Col(3)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(0, 0, 8, 0)
                    .Text(@vm.SizeInfo)
            );


    private void LabelStyle(TextBlock t) => t
        .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
        .VerticalAlignment(VerticalAlignment.Center)
        .FontSize(12);
}