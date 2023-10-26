using Pix2d.UI.Common;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.MainMenu.ViewModels;
using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public class LicenseView : ViewBaseSingletonVm<LicenseViewModel>
{
    protected override object Build(LicenseViewModel? vm)
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
                            .Text("License").FontSize(24)
                            .HorizontalAlignment(HorizontalAlignment.Left),

                        new Button()
                            .Background(Brushes.Transparent)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Command(vm?.ToggleProCommand)
                            .Padding(0)
                            .ContentTemplate(
                                new FuncDataTemplate<LicenseViewModel>((o, ns) =>
                                    new StackPanel()
                                        ._Children(new()
                                        {
                                            new TextBlock().Margin(0, 8, 0, 0)
                                                .Text("Current license:")
                                                .FontSize(16),
                                            new TextBlock()
                                                .Text(@vm.LicenseType, BindingMode.OneWay, bindingSource: vm)
                                                .FontSize(20)
                                            //.Foreground(StaticResources.Brushes.LinkHighlightBrush),
                                        }))),

                        new Grid()
                            .Margin(0, 24, 0, 0)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Cols("*,*")
                            .MinWidth(340)
                            ._Children(new()
                            {
                                new StackPanel()._Children(new()
                                {
                                    new Button()
                                        .Command(vm?.BuyProCommand)
                                        .Width(100)
                                        .Height(100)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Content(new Image().Source(StaticResources.ProImage)),

                                    new TextBlock()
                                        .Margin(4)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Text("Lifetime license"),
                                    new TextBlock().HorizontalAlignment(HorizontalAlignment.Left)
                                        .Margin(4)
                                        .Text("One time payment"),

                                    new TextBlock()
                                        .FontSize(14)
                                        .TextDecorations(TextDecorationCollection.Parse("Strikethrough"))
                                        .Text(@vm.OldPrice, BindingMode.OneWay, bindingSource: vm),
                                    new TextBlock()
                                        .FontSize(20)
                                        .Text(@vm.Price, BindingMode.OneWay, bindingSource: vm),

                                    new Button()
                                        .Command(vm?.BuyProCommand)
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
                                        .Items(
                                            new TextBlock().Text("✅ Unlimited layers"),
                                            new TextBlock().Text("✅ 100 undo steps"),
                                            new TextBlock().Text("✅ Advanced tools"),
                                            new TextBlock().Text("✅ Export to GIF"),
                                            new TextBlock().Text("   PNG Sprite sheets"),
                                            new TextBlock().Text("   PNG Sequences"),
                                            new TextBlock().Text("✅ Layer effects")
                                        )
                                }),
                                new StackPanel().Col(1)._Children(new()
                                {
                                    new Button()
                                        .Command(vm?.BuyUltimateCommand)
                                        .Width(100)
                                        .Height(100)
                                        .HorizontalAlignment(HorizontalAlignment.Left)
                                        .Margin(0, 0, 0, 48)
                                        .Content(new Image().Source(StaticResources.UltimateImage)),

                                    new StackPanel()
                                        .Orientation(Orientation.Horizontal)
                                        ._Children<StackPanel>(new()
                                        {
                                            new TextBlock()
                                                .FontSize(14)
                                                .TextDecorations(TextDecorationCollection.Parse("Strikethrough"))
                                                .Text(@vm.OldUltimatePrice, BindingMode.OneWay, bindingSource: vm),
                                            new TextBlock().Text("per month"),
                                        }),

                                    new StackPanel()
                                        .Orientation(Orientation.Horizontal)
                                        ._Children(new()
                                        {
                                            new TextBlock()
                                                .FontSize(20)
                                                .Text(@vm.UltimatePrice, BindingMode.OneWay, bindingSource: vm),
                                            new TextBlock().Text("per month"),
                                        }),
                                    new Button()
                                        .Command(vm?.BuyUltimateCommand)
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
                                        .Items(
                                            new TextBlock().Text("✅ All Pro features"),
                                            new TextBlock().Text("✅ Access to Premium assets library"),
                                            new TextBlock().Text("✅ Document tabs"),
                                            new TextBlock().Text("✅ Layout mode"),
                                            new TextBlock().Text("✅ Html5 export"),
                                            new TextBlock().Text("✅ Scripting and more...")
                                        )
                                })
                            }),

                        new Button()
                            .Margin(0, 8)
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Content("Read privacy policy")
                    })
            });

    protected override void OnCreated()
    {
        base.OnCreated();
        ViewModel?.OnLoad();
    }
}