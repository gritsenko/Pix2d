using System;
using Pix2d.Resources;
using Pix2d.Shared;
using Pix2d.ViewModels.MainMenu;

namespace Pix2d.Views.MainMenu;

public class NewDocumentView : ViewBase<NewDocumentSettingsViewModel>
{
    public NewDocumentView(NewDocumentSettingsViewModel viewModel) : base(viewModel)
    {
    }

    protected override object Build(NewDocumentSettingsViewModel vm) =>
        new Border()
            .Padding(32, 0, 0, 0)
            .Child(
                new StackPanel()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        new TextBlock()
                            .FontSize(24)
                            .Text("New"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Create new sprite"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Preset"),

                        new ComboBox()
                            .DataTemplates(
                                GetTextTemplate<NewDocumentSettingsPresetViewModel>(x => x?.Title ?? "")
                            )
                            .Margin(0, 8, 0, 0)
                            .MaxWidth(300)
                            .Items(vm.AvailablePresets)
                            .SelectedItem(@vm.SelectedPreset, BindingMode.TwoWay),

                        new SliderEx()
                            .Header("Width")
                            .Width(200)
                            .Units("px")
                            .Minimum(1)
                            .Maximum(1024)
                            .Value(@vm.Width, BindingMode.TwoWay),

                        new SliderEx()
                            .Header("Height")
                            .Width(200)
                            .Units("px")
                            .Minimum(1)
                            .Maximum(1024)
                            .Value(@vm.Height, BindingMode.TwoWay),

                        new Button()
                            .Content("Create")
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Margin(0,24,0,0)
                            .Background(StaticResources.Brushes.SelectedHighlighterBrush)
                            .Command(vm.CreateNewArtCommand)

                    ) //StackPanel.Children
            );

    private IDataTemplate GetTextTemplate<T>(Func<T, string> func) =>
        new FuncDataTemplate<T>((itemVm, ns) => new TextBlock().Text(func(itemVm)));
}