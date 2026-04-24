using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.UI.Resources;

namespace Pix2d.UI;

public partial class PromoBlockView : ViewBase<PromoBlockView.State>
{
    public PromoBlockView(ILicenseService? licenseService, AppState appState, ICommandService commandService)
        : base(new State(licenseService, appState, commandService))
    {
        this.Styles.AddRange(new IStyle[]
        {
            new Style<Button>(s => s.Class("wide").Descendant().Class("promo-grid")).Width(110),
            new Style<Button>(s => s.Class("small").Descendant().Class("promo-grid"))
                                        .Width(51)
                                        .IsVisible(false)
        });
    }

    protected override object Build(State state)
        => new Button()
            .Classes("promo-grid")
            .OnClick(_ => state.OpenPurchase())
            .Background(StaticResources.Brushes.SelectedItemBrush)
            .Content(
                new Grid()
                    .Cols("auto,auto")
                    .Children(
                        new TextBlock()
                            .Text(state, x => x.LicenseTypeText)
                            .Foreground(StaticResources.Brushes.LinkHighlightBrush)
                            .FontSize(18),
                        new TextBlock().VerticalAlignment(VerticalAlignment.Top).Col(1).Margin(new Thickness(1, 0, 0, 0)).Text(Suffix)
                    )
            )
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Stretch);

    public string CallToActionText { get; set; } = "PRO";
    public string Suffix { get; set; } = "";

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ILicenseService? _licenseService;

        [ObservableProperty]
        public partial string LicenseTypeText { get; set; } = string.Empty;

        public State(ILicenseService? licenseService, AppState appState, ICommandService commandService)
        {
            _licenseService = licenseService;
            _appState = appState;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;
            UpdateLicenseType();
            _appState.WatchFor(x => x.LicenseType, UpdateLicenseType);
        }

        public ViewCommands ViewCommands { get; }

        public void OpenPurchase()
        {
            Logger.Log("$Pressed to promo block");

            if (_licenseService == null)
                return;

            ViewCommands.ShowLicensePurchaseCommand.Execute();
        }

        private void UpdateLicenseType()
        {
            LicenseTypeText = _appState.LicenseType.ToString().ToUpperInvariant();
        }
    }
}