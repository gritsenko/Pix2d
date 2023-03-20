using Pix2d.ViewModels.License;

namespace Pix2d.Views;

public partial class PromoBlockView : ViewBaseSingletonVm<PromoBlockViewModel>
{
    protected override object Build(PromoBlockViewModel vm)
        => new Button()
            .Command(vm?.ShowLicenseInfoCommand)
            .Background(StaticResources.Brushes.SelectedItemBrush)
            .Content(Bind(vm.CallToActionText))
            .FontSize(18)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Foreground(StaticResources.Brushes.LinkHighlightBrush)
            .Width(51);

}