using Avalonia.Styling;
using Pix2d.Primitives;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public class PromoBlockView : ComponentBase
{
    public PromoBlockView()
    {
        this.Styles.AddRange(new IStyle[]
        {
            new Style<Button>(s => s.Class("wide").Descendant().Class("promo-grid")).Width(110),
            new Style<Button>(s => s.Class("small").Descendant().Class("promo-grid"))
                                        .Width(51)
                                        .IsVisible(false)
        });
    }

    protected override object Build()
        => new Button()
            .Classes("promo-grid")
            //.Command(ShowLicenseInfoCommand)
            .OnClick(e =>
            {
                Logger.Log("$Pressed to promo block");

                if (LicenseService == null)
                    return;

                Commands.View.ShowLicensePurchaseCommand.Execute();
            })
            .Background(StaticResources.Brushes.SelectedItemBrush)
            .Content(
                new Grid()
                    .Cols("auto,auto")
                    .Children(
                        new TextBlock()
                            .Text(@CallToActionText)
                            .Foreground(StaticResources.Brushes.LinkHighlightBrush)
                            .FontSize(18),
                        new TextBlock().VerticalAlignment(VerticalAlignment.Top).Col(1).Margin(new Thickness(1, 0, 0, 0)).Text(@Suffix)
                    )
            )
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Stretch);

    [Inject] public ILicenseService? LicenseService { get; } = null!;
    public string CallToActionText { get; set; } = "PRO";
    public string Suffix { get; set; } = "";

    protected override void OnAfterInitialized()
    {
        UpdateText();
    }

    private void UpdateText()
    {
        switch (LicenseService?.License)
        {
            case LicenseType.Pro:
                CallToActionText = "PRO";
                Suffix = "";
                break;
            case LicenseType.Ultimate:
                CallToActionText = "ULTIMATE";
                Suffix = "𝛽";
                break;
            default:
                CallToActionText = "ESS";
                Suffix = "";
                break;
        }

        StateHasChanged();
    }
}