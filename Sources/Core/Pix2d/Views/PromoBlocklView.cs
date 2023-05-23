namespace Pix2d.Views;

public class PromoBlockView : ComponentBase
{
    protected override object Build()
        => new Button()
            //.Command(ShowLicenseInfoCommand)
            .OnClick(e =>
            {
                Logger.Log("$Pressed to promo block");

                if (LicenseService == null)
                    return;

                Commands.View.ShowMainMenuCommand.Execute();
            })
            .Background(StaticResources.Brushes.SelectedItemBrush)
            .Content(Bind(CallToActionText))
            .FontSize(18)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Foreground(StaticResources.Brushes.LinkHighlightBrush)
            .Width(51);

    public ILicenseService? LicenseService { get; } = null!;
    public string CallToActionText { get; set; } = "PRO";

    protected override void OnAfterInitialized()
    {
        var isPro = LicenseService?.IsPro ?? true;
        CallToActionText = isPro ? "PRO" : "ESS";
        StateHasChanged();
    }

}