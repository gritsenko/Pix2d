using Pix2d.Resources;
using Pix2d.Shared;
using Pix2d.ViewModels.Preview;

namespace Pix2d.Views;

public class ArtworkPreviewView : ViewBaseSingletonVm<ArtworkPreviewViewModel>
{
    protected override object Build(ArtworkPreviewViewModel vm) =>
        new Grid()
            .Rows("*,Auto")
            .Children(
                new ScrollViewer()
                    .Background(StaticResources.Brushes.CheckerTilesBrush)
                    .Content(
                        new SKImageView()
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Source(@vm.Preview)

                        ),

                new Grid().Row(1).HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new ComboBox()
                            .Items(vm.AvailableScales)
                            .SelectedItem(@vm.SelectedScaleItem, BindingMode.TwoWay)
                            .ItemTemplate(_itemTemplate)
                    )
            );

    private IDataTemplate _itemTemplate = 
        new FuncDataTemplate<ScaleItem>((itemVm, ns) 
            => new TextBlock().Text(itemVm.Title));
}