using Avalonia.Interactivity;
using Avalonia.Styling;
using Pix2d.Abstract.Export;
using Pix2d.Common;
using Pix2d.Infrastructure.Tasks;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using SkiaNodes.Extensions;
using SkiaSharp;
using Pix2d.Command;
using Pix2d.Plugins.PngFormat.Exporters;

namespace Pix2d.UI.Export;

public class ExportView : ComponentBase
{
    public const string PreviewName = "export-preview";
    public const string SettingsName = "export-settings";

    private readonly IDataTemplate _itemTemplate =
        new FuncDataTemplate<IExporter>((itemVm, ns)
            => new TextBlock().Text(itemVm?.Title ?? ""));

    protected override StyleGroup BuildStyles() =>
    [
        new Style<ScrollViewer>(s => VisualStates.Wide().Name(ExportView.PreviewName))
            .Col(0).ColSpan(1)
            .Row(0).RowSpan(2),
        new Style<ScrollViewer>(s => VisualStates.Wide().Name(ExportView.SettingsName))
            .Col(1).ColSpan(1)
            .Row(0).RowSpan(2)
            .Margin(16, 0),

        new Style<ScrollViewer>(s => VisualStates.Narrow().Name(ExportView.PreviewName))
            .Col(0).ColSpan(2)
            .Row(0).RowSpan(1),

        new Style<ScrollViewer>(s => VisualStates.Narrow().Name(ExportView.SettingsName))
            .Col(0).ColSpan(2)
            .Row(1).RowSpan(1)
            .Margin(0, 16),

        new Style<ExportProWarningView>(s => VisualStates.Narrow().OfType<ExportProWarningView>())
            .ColSpan(2)
    ];

    protected override object Build() =>
        new Grid().Rows("auto,*").Cols("*,auto")
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(
                new TextBlock().FontSize(24).VerticalAlignment(VerticalAlignment.Center).Margin(16, 0)
                    .Text("Export artwork"),
                new Button().Col(1).Content("X").Height(40).Width(40).Command(ViewCommands.HideExportDialogCommand),
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
                                                    .ItemTemplate<ExporterInfo>(item =>
                                                        new TextBlock().Text(item?.Name ?? ""))
                                                    .ItemsSource(Exporters)
                                                    .SelectedItem(() => SelectedExporterInfo,
                                                        v => SelectedExporterInfo = (ExporterInfo)v),
                                                new ContentControl()
                                                    .Ref(out _exporterSettingsControl),
                                                new SliderEx()
                                                    .Label("Image scale")
                                                    .Units("x")
                                                    .Minimum(1)
                                                    .Maximum(20)
                                                    .Value(() => Scale, v => Scale = (double)v!)
                                            )
                                    ),
                                new Grid().ColSpan(2).Row(1)
                                    .Rows("Auto,Auto")
                                    .Cols("*,Auto,Auto")
                                    .VerticalAlignment(VerticalAlignment.Bottom)
                                    .Children(
                                        new Button().Row(0).Col(1).ColSpan(2)
                                            .Classes("btn")
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                            .Width(110)
                                            .Background(StaticResources.Brushes.SelectedItemBrush)
                                            .Margin(new Thickness(0, 10))
                                            .IsVisible(PlatformStuffService.CanShare)
                                            .Content(new StackPanel().Orientation(Orientation.Horizontal).Children(
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                                    .Margin(new Thickness(0, 0, 8, 0))
                                                    .Text("\xE72D"),
                                                new TextBlock().Text("Share"))
                                            )
                                            .OnClick(Share),
                                        new Button().Row(1).Col(1)
                                            .Classes("btn")
                                            .Content("Save")
                                            .Width(110)
                                            .Margin(0, 0, 20, 0)
                                            .Background(StaticResources.Brushes.BrushButtonBrush)
                                            .OnClick(_ => OnExportCommandExecute()),
                                        new Button().Row(1).Col(2)
                                            .Classes("btn")
                                            .Content("Cancel")
                                            .Width(110)
                                            .Command(ViewCommands.HideExportDialogCommand)
                                    )
                                //new ExportProWarningView()
                                // .IsVisible(() => !AppState.IsPro)
                            )
                    ));

    [Inject] IExportService ExportService { get; set; } = null!;

    [Inject] IPlatformStuffService PlatformStuffService { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;

    [Inject] private ICommandService CommandService { get; set; } = null!;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;

    private ExporterInfo _selectedExporterInfo;

    private double _scale = 1;
    private ContentControl _exporterSettingsControl;
    private IExporter? _configuredExporter;

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

    public ExporterInfo SelectedExporterInfo
    {
        get => _selectedExporterInfo;
        set
        {
            if (_selectedExporterInfo != value)
            {
                _selectedExporterInfo = value;
                UpdateSettingsControl(_selectedExporterInfo);
                UpdatePreview();
                OnPropertyChanged();
                StateHasChanged();
            }
        }
    }

    private SKBitmapObservable Preview { get; } = new();

    private IReadOnlyList<ExporterInfo> Exporters => ExportService.RegisteredExporters;

    protected override void OnAfterInitialized()
    {
        SelectedExporterInfo = Exporters.First();
        AppState.UiState.WatchFor(x => x.ShowExportDialog,
            () =>
            {
                if (AppState.UiState.ShowExportDialog)
                    UpdatePreview();
            });

        AppState.UiState.WatchFor(x => x.PreferredExportFormat,
            () => SelectExporter(AppState.UiState.PreferredExportFormat));
    }

    private void UpdateSettingsControl(ExporterInfo exporterInfo)
    {
        // Create a temporary exporter instance for configuration
        _configuredExporter = exporterInfo.CreateInstanceFunc();
        
        if (exporterInfo.ExporterType == typeof(SpritesheetImageExporter))
        {
            var settingsView = new SpritesheetExportSettingsView();
            settingsView.Exporter = (SpritesheetImageExporter)_configuredExporter;
            _exporterSettingsControl.Content = settingsView;
        }
        else if (exporterInfo.ExporterType == typeof(SpritePngSequenceExporter))
        {
            var settingsView = new SpritePngSequenceExporterSettingsView();
            settingsView.Exporter = (SpritePngSequenceExporter)_configuredExporter;
            _exporterSettingsControl.Content = settingsView;
        }
        else
        {
            _configuredExporter = null;
            _exporterSettingsControl.Content = null;
        }
    }

    private async void OnExportCommandExecute()
    {
        try
        {
            using var uiBlocker = new UiBlocker("Exporting...");
            Logger.LogEventWithParams("Exporting image", new Dictionary<string, string?>
            {
                { "Exporter", SelectedExporterInfo.Name }
            });

            var nodesToExport = ExportService.GetNodesToExport(Scale);
            
            // Use configured exporter if available, otherwise use the standard approach
            if (_configuredExporter != null)
            {
                await ExportService.ExportNodesAsync(nodesToExport, Scale, _configuredExporter);
            }
            else
            {
                await ExportService.ExportNodesAsync(nodesToExport, Scale, SelectedExporterInfo);
            }
            
            ViewCommands.HideExportDialogCommand.Execute();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private void UpdatePreview()
    {
        if (AppState.CurrentProject.CurrentNodeEditor is not SpriteEditor spriteEditor) return;

        var nodesToExport = ExportService.GetNodesToExport(Scale);
        var preview = nodesToExport.ToArray()
            .RenderToBitmap(
                spriteEditor.CurrentSprite.UseBackgroundColor
                    ? spriteEditor.CurrentSprite.BackgroundColor
                    : SKColor.Empty, Scale);

        Preview.SetBitmap(preview);
    }

    private void SelectExporter(string format)
    {
        //if (string.IsNullOrWhiteSpace(format))
        //    return;

        //var selected = Exporters.FirstOrDefault(x => x.SupportedExtensions.Contains(format));
        //if (selected != default)
        //{
        //    SelectedExporter = selected;
        //}
    }

    private void Share(RoutedEventArgs _)
    {
        //var exporter = SelectedExporter as IStreamExporter ?? Exporters.OfType<IStreamExporter>().FirstOrDefault();
        //if (exporter == null)
        //{
        //    Logger.Log("Could not find suitable exporter.");
        //    return;
        //}

        //PlatformStuffService.Share(exporter, Scale);
        ViewCommands.HideExportDialogCommand.Execute();
    }
}