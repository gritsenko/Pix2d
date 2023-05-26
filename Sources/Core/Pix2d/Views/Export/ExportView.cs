using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Shared;
using Pix2d.ViewModels.Export;
using Pix2d.ViewModels.Preview;
using SkiaNodes;
using SkiaSharp;
using System.Collections.Generic;
using System.Windows.Input;
using System;
using System.Linq;
using Mvvm;
using Pix2d.Abstract.Export;
using SkiaNodes.Extensions;

namespace Pix2d.Views.Export;

public class ExportView : ComponentBase
{
    private readonly IDataTemplate _itemTemplate =
        new FuncDataTemplate<IExporter>((itemVm, ns)
            => new TextBlock().Text(itemVm?.Title ?? ""));

    protected override object Build() =>
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
                                    .Source(Preview, BindingMode.OneWay) //!!!!!!!
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
                                            .ItemsSource(Exporters)
                                            .SelectedItem(Bind(SelectedExporter, BindingMode.TwoWay))

                                    //new StackPanel() //Exporter options
                                    //    .DataContext(@vm.SelectedExporter, out var selectedExporter)
                                    //    .Children(
                                    //        new TextBlock().Margin(0, 8, 0, 0).Text("File name prefix")
                                    //            .IsVisible(@selectedExporter.ShowFileName),
                                    //        new TextBox()
                                    //            .Watermark("File Name Prefix")
                                    //            .IsVisible(@selectedExporter.ShowFileName),

                                    //        new TextBlock().Margin(0, 8, 0, 0).Text("Columns count")
                                    //            .IsVisible(@selectedExporter.ShowSpritesheetOptions),

                                    //        new NumericUpDown()
                                    //            .Watermark("Columns count")
                                    //            .Minimum(1)
                                    //            .Value(new Binding("Columns", BindingMode.TwoWay))
                                    //            .IsVisible(@selectedExporter.ShowSpritesheetOptions)
                                    //    ), // exporter options
                                    //new SliderEx()
                                    //    .Header("Image scale")
                                    //    .Units("x")
                                    //    .Minimum(1)
                                    //    .Maximum(20)
                                    //    .DataContext(@vm.ExportSettingsViewModel, out var exportSettingsViewModel)
                                    //    .Value(@exportSettingsViewModel.Scale, BindingMode.TwoWay)
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
                                    .Command(ExportCommand),
                                new Button().Col(2)
                                    .Content("Cancel")
                                    .Width(110)
                                    .Background(StaticResources.Brushes.SelectedItemBrush)
                                    .Command(Commands.View.HideExportDialogCommand)
                            )
                    )
            );

    [Inject] IExportService ExportService { get; set; }

    [Inject] IBusyController BusyController { get; set; }
    [Inject] AppState AppState { get; set; }
    [Inject] IMessenger Messenger { get; set; }


    private ExportSettingsViewBase _selectedExporter;
    public ExportSettingsViewBase SelectedExporter
    {
        get => _selectedExporter;
        set
        {
            if (_selectedExporter != value)
            {
                _selectedExporter = value;
                UpdatePreview();
                OnPropertyChanged();
            }
        }
    }

    public SKBitmapObservable Preview { get; } = new();

    public List<IExporter> Exporters => ExportService.Exporters.ToList();

    public ICommand ExportCommand => new LoggedRelayCommand(OnExportCommandExecute, () => true, "Exported image");

    protected override void OnAfterInitialized()
    {
        Messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(x => x.ShowExportDialog, () =>
        {
            if (AppState.UiState.ShowExportDialog)
                UpdatePreview();
        }));
    }

    private async void OnExportCommandExecute()
    {
        await BusyController.RunLongTaskAsync(async () =>
        {
            try
            {
                Logger.LogEventWithParams("Exporting image", new Dictionary<string, string> { { "Exporter", SelectedExporter.Name } });

                var nodesToExport = GetNodesToExport();
                await ExportService.ExportNodesAsync(nodesToExport, SelectedExporter);
                //await SelectedExporter.Export(nodesToExport, new );

                Commands.View.HideExportDialogCommand.Execute();
            }
            catch (OperationCanceledException canceledException)
            {
                //export canceled, just return to dialog
            }
        });
    }

    private IEnumerable<SKNode> GetNodesToExport()
    {
        if (CoreServices.EditService.CurrentEditedNode == null)
            yield break;

        yield return CoreServices.EditService.CurrentEditedNode;
    }

    private void UpdatePreview()
    {
        var settings = new ImageExportSettings();
        var editorService = CoreServices.EditService;

        if (!(editorService.GetCurrentEditor() is SpriteEditor spriteEditor) || settings == null)
            return;

        var nodesToExport = GetNodesToExport();
        var preview = nodesToExport.ToArray()
            .RenderToBitmap(
                spriteEditor.CurrentSprite.UseBackgroundColor ? spriteEditor.CurrentSprite.BackgroundColor : SKColor.Empty,
                settings.Scale);
        Preview.SetBitmap(preview);
    }

}