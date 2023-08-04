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
            .VerticalAlignment(VerticalAlignment.Stretch)
            .Width(51);

    public ILicenseService? LicenseService { get; } = null!;
    public string CallToActionText { get; set; } = "PRO";
    public string Suffix { get; set; } = "";

    protected override void OnAfterInitialized()
    {
        var isPro = LicenseService?.IsPro ?? true;
        #if BETA
        CallToActionText = "ULT";
        Suffix = "𝛽";
        #else
        CallToActionText = isPro ? "PRO" : "ESS";
        #endif
        
        StateHasChanged();
    }

}