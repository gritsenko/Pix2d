using Pix2d.Shared;
using Pix2d.ViewModels.Export;

namespace Pix2d.Views;

public class ExportView : ViewBaseSingletonVm<ExportPageViewModel>
{
    protected override object Build(ExportPageViewModel vm) =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Padding(16)
            .Child(
                new Grid()
                    .Cols("*,*")
                    .MinWidth(200)
                    .MinHeight(200)
                    .Children(
                        new ScrollViewer()
                            .Background(StaticResources.Brushes.MainBackgroundBrush)
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Content(
                                new SKImageView()
                                    .ShowCheckerBackground(true)
                                    .Source(@vm.Preview, BindingMode.OneWay)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),

                        new ScrollViewer().Col(1) //Properties panel
                            .Margin(16)
                            .Content(
                                new StackPanel()
                                    .Children(
                                        new TextBlock().Text("Exporter"),
                                        new ComboBox()
                                            .ItemTemplate(_itemTemplate)
                                            .ItemsSource(vm.Exporters)
                                            .SelectedItem(@vm.SelectedExporter, BindingMode.TwoWay),

                                        new StackPanel() //Exporter options
                                            .DataContext(@vm.SelectedExporter, out var selectedExporter)
                                            .Children(
                                                new TextBlock().Margin(0, 8, 0, 0).Text("File name prefix")
                                                    .IsVisible(@selectedExporter.ShowFileName),
                                                new TextBox()
                                                    .Watermark("File Name Prefix")
                                                    .IsVisible(@selectedExporter.ShowFileName),

                                                new TextBlock().Margin(0, 8, 0, 0).Text("Columns count")
                                                    .IsVisible(@selectedExporter.ShowSpritesheetOptions),

                                                new NumericUpDown()
                                                    .Watermark("Columns count")
                                                    .Minimum(1)
                                                    .Value(new Binding("Columns", BindingMode.TwoWay))
                                                    .IsVisible(@selectedExporter.ShowSpritesheetOptions)
                                            ), // exporter options
                                        new SliderEx()
                                            .Header("Image scale")
                                            .Units("x")
                                            .Minimum(1)
                                            .Maximum(20)
                                            .DataContext(@vm.ExportSettingsViewModel, out var exportSettingsViewModel)
                                            .Value(@exportSettingsViewModel.Scale, BindingMode.TwoWay)
                                    )
                            ),
                        new Grid().Col(1)
                            .Cols("*,Auto,Auto")
                            .VerticalAlignment(VerticalAlignment.Bottom)
                            .Children(
                                new Button().Col(1)
                                    .Content("Save")
                                    .Width(110)
                                    .Margin(0, 0, 20, 0)
                                    .Background(StaticResources.Brushes.SelectedItemBrush)
                                    .Command(vm.ExportCommand),
                                new Button().Col(2)
                                    .Content("Cancel")
                                    .Width(110)
                                    .Background(StaticResources.Brushes.SelectedItemBrush)
                                    .Command(vm.CancelCommand)
                            )
                    )
            );

    private IDataTemplate _itemTemplate =
        new FuncDataTemplate<ExporterViewModel>((itemVm, ns)
            => new TextBlock().Text(@itemVm?.Name ?? "-"));

}