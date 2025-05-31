using System.Diagnostics;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pix2d.UI.Common;
using Pix2d.UI.Resources;
using System.Globalization;
using System.Text.RegularExpressions;
using Pix2d.Common.Extensions;
using Pix2d.UI.Shared;
using Pix2d.Command;

namespace Pix2d.UI.MainMenu;

public class LicenseView : LocalizedComponentBase
{
    private FuncDataTemplate<string> FeatureItemDataTemplate => new((model, ns) =>
        new Grid().Cols("20,*")
            .Children(
                new TextBlock().Text("✅"),
                new TextBlock().Col(1).Text(model)
            )
    );

    protected override object Build()
        => new Grid()
            .Rows("Auto,*,Auto")
            ._Children<Grid>(new()
            {
                new StackPanel().Margin(24, 0, 0, 0)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    ._Children<StackPanel>(new()
                    {
                        new TextBlock()
                            .Margin(0, 8, 0, 0)
                            .Text(L("License")).FontSize(24)
                            .HorizontalAlignment(HorizontalAlignment.Left),

                        new Button()
                            .Background(Brushes.Transparent)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .OnClick(_ => OnToggleProClicked())
                            .Padding(0)
                            .Content(
                                new StackPanel()
                                    ._Children(new()
                                    {
                                        new TextBlock().Margin(0, 8, 0, 0)
                                            .HorizontalAlignment(HorizontalAlignment.Left)
                                            .Text(L("Current license:"))
                                            .FontSize(16),
                                        new TextBlock()
                                            .HorizontalAlignment(HorizontalAlignment.Left)
                                            .Text(() => LicenseType)
                                            .FontSize(20)
                                        //.Foreground(StaticResources.Brushes.LinkHighlightBrush),
                                    })
                            ),

                        new Grid()
                            .Margin(0, 24, 0, 0)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Cols("*,*")
                            .Rows("Auto,*")
                            .MinWidth(340)
                            ._Children(new()
                            {
                                new StackPanel().Margin(new Thickness(0, 0, 24, 0))._Children(new()
                                {
                                    new Button()
                                        .OnClick(_ => OnBuyProClicked())
                                        .Margin(0)
                                        .Padding(0)
                                        .Width(100)
                                        .Height(100)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Content(new Image().Source(StaticResources.ProImage)),

                                    new TextBlock()
                                        .Margin(0, 4)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Text(L("Lifetime license")),
                                    new TextBlock().HorizontalAlignment(HorizontalAlignment.Left)
                                        .Margin(0, 4)
                                        .Text(L("One time payment"))
                                }),
                                new StackPanel().Row(1).Margin(new Thickness(0, 0, 24, 0))
                                    .HorizontalAlignment(HorizontalAlignment.Left)._Children(new()
                                    {
                                        new TextBlock()
                                            .FontSize(14)
                                            .TextDecorations(TextDecorationCollection.Parse("Strikethrough"))
                                            .Text(() => OldPrice),
                                        new TextBlock()
                                            .FontSize(20)
                                            .Text(() => Price),

                                        new Button()
                                            .IsEnabled(() => !AppState.IsPro)
                                            .OnClick(_ => OnBuyProClicked())
                                            .Margin(0, 8)
                                            .Background("#FFFFD200".ToColor().ToBrush())
                                            .Foreground("#FF3B2300".ToColor().ToBrush())
                                            .FontSize(16)
                                            .FontWeight(FontWeight.Bold)
                                            .Padding(8)
                                            .HorizontalAlignment(HorizontalAlignment.Left)
                                            .Content("Buy now"),

                                        new ItemsControl()
                                            .HorizontalAlignment(HorizontalAlignment.Left)
                                            .VerticalAlignment(VerticalAlignment.Top)

                                            .Foreground(Brushes.White)
                                            .ItemTemplate(FeatureItemDataTemplate)
                                            .ItemsSource(new[] { "Up to 100 undo steps", "New features early access" })
                                    }),
                                new StackPanel().Col(1)._Children(new()
                                {
                                    new Button()
                                        .OnClick(_ => OnBuyUltimateClicked())
                                        .Width(100)
                                        .Height(100)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Margin(0)
                                        .Padding(0)
                                        .Content(new Image().Source(StaticResources.UltimateImage)),

                                }),
                                new StackPanel().Col(1).Row(1).Margin(new Thickness(0, 0, 24, 0))._Children(new()
                                {

                                    new StackPanel()
                                        .Orientation(Orientation.Horizontal)
                                        ._Children<StackPanel>(new()
                                        {
                                            new TextBlock()
                                                .FontSize(14)
                                                .TextDecorations(TextDecorationCollection.Parse("Strikethrough"))
                                                .Text(() => OldUltimatePrice),
                                            new TextBlock().Margin(4, 0, 0, 0)
                                                .VerticalAlignment(VerticalAlignment.Bottom).Text(L("per month")),
                                        }),

                                    new StackPanel()
                                        .Orientation(Orientation.Horizontal)
                                        ._Children(new()
                                        {
                                            new TextBlock()
                                                .FontSize(20)
                                                .Text(() => UltimatePrice),
                                            new TextBlock().Margin(4, 0, 0, 2)
                                                .VerticalAlignment(VerticalAlignment.Bottom).Text(L("per month")),
                                        }),
                                    new Button()
                                        .OnClick(_ => OnBuyUltimateClicked())
                                        .Margin(0, 8)
                                        .Background("#FFCCCCCC".ToColor().ToBrush())
                                        .Foreground("#FF3B2300".ToColor().ToBrush())
                                        .FontSize(16)
                                        .FontWeight(FontWeight.Bold)
                                        .Padding(8)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Content("Pre-order"),

                                    new ItemsControl()
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .VerticalAlignment(VerticalAlignment.Top)
                                        .Foreground(Brushes.White)
                                        .ItemTemplate(FeatureItemDataTemplate)
                                        .ItemsSource(new[]
                                            {
                                                "All Pro features",
                                                "Access to Premium assets library",
                                                "Document tabs",
                                                "Layout mode",
                                                "Html5 export",
                                                "Scripting and more..."
                                            }
                                        )
                                })
                            }),

                        new Button()
                            .Margin(0, 8)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Read privacy policy")
                            .OnClick(OpenPrivacyPolicy)
                    })
            });


    [Inject] private ILicenseService LicenseService { get; set; } = null!;
    [Inject] private IPlatformStuffService PlatformStuffService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;

    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;

    public string? LicenseType => AppState.LicenseType.ToString();

    public string? Price { get; set; }
    public string? UltimatePrice { get; set; }
    public string? OldUltimatePrice { get; set; }

    public string? OldPrice { get; set; }


    [Conditional("DEBUG")]
    public void OnToggleProClicked()
    {
        AppState.LicenseType =
            AppState.IsPro ? Pix2d.Primitives.LicenseType.Essentials : Pix2d.Primitives.LicenseType.Pro;
        Logger.Log("$On license changed to " + (AppState.IsPro ? "PRO" : "ESS"));
    }

    private void OpenPrivacyPolicy(RoutedEventArgs _)
    {
        PlatformStuffService.OpenUrlInBrowser("https://pix2d.com/privacy.html");
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsVisibleProperty && (bool)(change.NewValue ?? false))
        {
            OnLoad();
        }
    }

    protected override void OnAfterInitialized()
    {
        AppState.WatchFor(x => x.LicenseType, StateHasChanged);
    }

    public void OnLoad()
    {
        Logger.Log("$License view opened");
        GeneratePrice();
    }


    private async void OnBuyProClicked()
    {
        if (AppState.IsPro)
        {
            return;
        }
        
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                Logger.Log("$Pressed buy/restore PRO on Main Menu");
                if (await LicenseService.BuyPro())
                {
                    DialogService.Alert("Thank you! Now you are PRO user!", "Success!");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("$Error on buy PRO on Main Menu: " + ex.Message);
                Logger.LogException(ex);
            }
            finally
            {
                ViewCommands.HideMainMenuCommand.Execute();
            }

        });
    }

    private async void OnBuyUltimateClicked()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                Logger.Log("$Pressed pre-order ULTIMATE on Main Menu");
                var result = await DialogService.ShowYesNoDialog(
                    "With buying PRO version, you will get ULTIMATE edition for free, on it's release. Do you want to proceed with PRO purchase?",
                    "Pre-order Pix2D ULTIMATE", "Yes", "No");

                if (result)
                {
                    Logger.Log("$Pressed to buy PRO to ULTIMATE on Main Menu");
                    if (await LicenseService.BuyPro())
                    {
                        DialogService.Alert("Thank you! Now you are PRO/ULTIMATE user!", "Success!");
                    }
                }
                else
                {
                    Logger.Log("$Pressed No in pre-order message box PRO on Main Menu");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("$Error on buy PRO(pre-order ULTIMATE) on Main Menu: " + ex.Message);
                Logger.LogException(ex);
            }
            finally
            {
                ViewCommands.HideMainMenuCommand.Execute();
            }
        });
    }

    private void GeneratePrice()
    {
        try
        {
            var price = LicenseService.GetFormattedPrice;
            Price = price;
            var match = Regex.Match(price, @"\d+(,\d+)*(\.\d+)");

            double val = 0;
            if (match.Success && double.TryParse(match.Value, CultureInfo.InvariantCulture, out val))
            {
                var pattern = price.Replace(match.Value, "%price");
                OldPrice = price;
                var oldPrice = Math.Round(val * 2.5 / 10) * 10;
                var ultimatePrice = Math.Round(oldPrice * 1.5);
                var oldUltimatePrice = Math.Round(ultimatePrice * 2.5 / 10) * 10;

                OldPrice = pattern.Replace("%price", oldPrice.ToString("#.00"));
                UltimatePrice = pattern.Replace("%price", ultimatePrice.ToString("#.00"));
                OldUltimatePrice = pattern.Replace("%price", oldUltimatePrice.ToString("#.00"));
            }
        }
        catch (Exception exception)
        {
            Logger.LogException(exception);
        }

        StateHasChanged();
    }

}