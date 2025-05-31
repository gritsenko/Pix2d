using Avalonia.Styling;
using Pix2d.Command;
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

                ViewCommands.ShowLicensePurchaseCommand.Execute();
            })
            .Background(StaticResources.Brushes.SelectedItemBrush)
            .Content(
                new Grid()
                    .Cols("auto,auto")
                    .Children(
                        new TextBlock()
                            .Text(() => $"{AppState?.LicenseType.ToString().ToUpper()}")
                            .Foreground(StaticResources.Brushes.LinkHighlightBrush)
                            .FontSize(18),
                        new TextBlock().VerticalAlignment(VerticalAlignment.Top).Col(1).Margin(new Thickness(1, 0, 0, 0)).Text(@Suffix)
                    )
            )
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Stretch);

    [Inject] public ILicenseService? LicenseService { get; } = null!;
    [Inject] public AppState? AppState { get; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;
    public string CallToActionText { get; set; } = "PRO";
    public string Suffix { get; set; } = "";

    protected override void OnAfterInitialized()
    {
        AppState.WatchFor(x => x.LicenseType, StateHasChanged);
    }
}