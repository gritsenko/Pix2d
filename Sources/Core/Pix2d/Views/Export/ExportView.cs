using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Shared;
using SkiaNodes;
using SkiaSharp;
using System.Collections.Generic;
using System.Windows.Input;
using System;
using System.Linq;
using Mvvm;
using Pix2d.Abstract.Export;
using Pix2d.Exporters;
using SkiaNodes.Extensions;

namespace Pix2d.Views.Export;

public class ExportView : ComponentBase
{
    public const string PreviewName = "export-preview";
    public const string SettingsName = "export-settings";
    
    private readonly IDataTemplate _itemTemplate =
        new FuncDataTemplate<IExporter>((itemVm, ns)
            => new TextBlock().Text(itemVm?.Title ?? ""));

    protected override object Build() =>
        new Grid().Rows("auto,*").Cols("*,auto")
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(
                new TextBlock().FontSize(24).VerticalAlignment(VerticalAlignment.Center).Margin(16, 0).Text("Export artwork"),
                new Button().Col(1).Content("X").Height(40).Width(40).Command(Commands.View.HideExportDialogCommand),
        new Border().Row(1).ColSpan(2)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Padding(16)
            .Child(
                new Grid()
                    .Rows("*,*")
                    .Cols("*,*")
                    .MinWidth(200)
                    .MinHeight(200)
                    .Children(
                        new ScrollViewer()
                            .Background(StaticResources.Brushes.MainBackgroundBrush)
                            .Name(PreviewName)
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .Content(
                                new SKImageView()
                                    .ShowCheckerBackground(true)
                                    .Source(Preview) //!!!!!!!
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .VerticalAlignment(VerticalAlignment.Center)
                            ),

                        new ScrollViewer()
                            .Name(SettingsName)
                            .Content(
                                new StackPanel()
                                    .Spacing(8)
                                    .Children(
                                        new TextBlock().Text("Export type"),
                                        new ComboBox()
                                            .ItemTemplate(_itemTemplate)
                                            .ItemsSource(Exporters)
                                            .SelectedItem(Bind(SelectedExporter, BindingMode.TwoWay)),
                                        new ContentControl()
                                            .Ref(out _exporterSettingsControl),

                                        new SliderEx()
                                        .Header("Image scale")
                                        .Units("x")
                                        .Minimum(1)
                                        .Maximum(20)
                                        .Value(Scale, BindingMode.TwoWay, bindingSource: this)
                                    )
                            ),
                        new Grid().ColSpan(2).Row(1)
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
            ));

    [Inject] IExportService ExportService { get; set; }

    [Inject] IBusyController BusyController { get; set; }
    [Inject] AppState AppState { get; set; }
    [Inject] IMessenger Messenger { get; set; }

    private IExporter _selectedExporter;

    private double _scale = 1;
    private ContentControl _exporterSettingsControl;

    public double Scale
    {
        get => _scale;
        set
        {
            if (value.Equals(_scale)) return;
            _scale = value;
            UpdatePreview();
            OnPropertyChanged();
        }
    }

    public IExporter SelectedExporter
    {
        get => _selectedExporter;
        set
        {
            if (_selectedExporter != value)
            {
                _selectedExporter = value;
                UpdateSettingsControl(_selectedExporter);
                UpdatePreview();
                OnPropertyChanged();
            }
        }
    }

    private void UpdateSettingsControl(IExporter selectedExporter)
    {
        if (selectedExporter is SpritesheetImageExporter spritesheetImageExporter)
            _exporterSettingsControl.Content = new SpritesheetExportSettingsView().Exporter(spritesheetImageExporter);
        else if (selectedExporter is SpritePngSequenceExporter pngSequenceExporter)
            _exporterSettingsControl.Content = new SpritePngSequenceExporterSettingsView().Exporter(pngSequenceExporter);
        else
            _exporterSettingsControl.Content = null;
    }

    public SKBitmapObservable Preview { get; } = new();

    public List<IExporter> Exporters => ExportService.Exporters.ToList();

    public ICommand ExportCommand => new LoggedRelayCommand(OnExportCommandExecute, () => true, "Exported image");

    protected override void OnAfterInitialized()
    {
        SelectedExporter = Exporters.First();
        Messenger.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(x => x.ShowExportDialog, () =>
        {
            if (AppState.UiState.ShowExportDialog)
                UpdatePreview();
        }));
        
        AppState.UiState.WatchFor(x => x.PreferredExportFormat, () => SelectExporter(AppState.UiState.PreferredExportFormat));
    }

    private async void OnExportCommandExecute()
    {
        await BusyController.RunLongTaskAsync(async () =>
        {
            try
            {
                Logger.LogEventWithParams("Exporting image", new Dictionary<string, string> { { "Exporter", SelectedExporter.Title } });

                var nodesToExport = GetNodesToExport();
                await ExportService.ExportNodesAsync(nodesToExport, 1, SelectedExporter);
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
        var editorService = CoreServices.EditService;

        if (!(editorService.GetCurrentEditor() is SpriteEditor spriteEditor))
            return;

        var nodesToExport = GetNodesToExport();
        var preview = nodesToExport.ToArray()
            .RenderToBitmap(
                spriteEditor.CurrentSprite.UseBackgroundColor ? spriteEditor.CurrentSprite.BackgroundColor : SKColor.Empty, Scale);
        Preview.SetBitmap(preview);
    }

    private void SelectExporter(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return;
        
        var selected = Exporters.FirstOrDefault(x => x.SupportedExtensions.Contains(format));
        if (selected != default)
        {
            SelectedExporter = selected;
        }
    }
}