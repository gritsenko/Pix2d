using Avalonia.Styling;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Export;
using Pix2d.Command;
using Pix2d.Common;
using Pix2d.Infrastructure.Tasks;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.UI.Export;

public partial class ExportView : ViewBase<ExportView.State>
{
    public const string PreviewName = "export-preview";
    public const string SettingsName = "export-settings";

    public ExportView(IExportService exportService, IPlatformStuffService platformStuffService, AppState appState, ICommandService commandService)
        : base(new State(exportService, platformStuffService, appState, commandService))
    {
    }

    protected override StyleGroup BuildStyles() =>
    [
        new StyleGroup(_ => VisualStates.Wide())
        {
            new Style<ScrollViewer>(s => s.Name(ExportView.PreviewName))
                .Col(0).ColSpan(1)
                .Row(0).RowSpan(2),
            new Style<ScrollViewer>(s => s.Name(ExportView.SettingsName))
                .Col(1).ColSpan(1)
                .Row(0).RowSpan(2)
                .Margin(16, 0)
        },

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<ScrollViewer>(s => s.Name(ExportView.PreviewName))
                .Col(0).ColSpan(2)
                .Row(0).RowSpan(1),
            new Style<ScrollViewer>(s => s.Name(ExportView.SettingsName))
                .Col(0).ColSpan(2)
                .Row(1).RowSpan(1)
                .Margin(0, 16),
            new Style<ExportProWarningView>()
                .ColSpan(2)
        }
    ];

    protected override object Build(State state) =>
        new Grid().Rows("auto,*").Cols("*,auto")
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(
                new TextBlock().FontSize(24).VerticalAlignment(VerticalAlignment.Center).Margin(16, 0)
                    .Text("Export artwork"),
                new Button().Col(1).Content("X").Height(40).Width(40).Command(state.ViewCommands.HideExportDialogCommand),
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
                                            .Source(state.Preview)
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
                                                        new TextBlock().Text(item?.Name ?? string.Empty))
                                                    .ItemsSource(state.Exporters)
                                                    .SelectedItem(state, x => x.SelectedExporterInfo, BindingMode.TwoWay),
                                                new ContentControl()
                                                    .Content(state, x => x.ExporterSettingsContent!),
                                                new SliderEx()
                                                    .Label("Image scale")
                                                    .Units("x")
                                                    .Minimum(1)
                                                    .Maximum(20)
                                                    .Value(state, x => x.Scale, BindingMode.TwoWay)
                                            )
                                    ),
                                new StackPanel().ColSpan(2).Row(1)
                                    .Orientation(Orientation.Horizontal)
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                    .VerticalAlignment(VerticalAlignment.Bottom)
                                    .Children(
                                        new Button()
                                            .Classes("btn")
                                            .Content("Save")
                                            .Width(110)
                                            .Margin(0, 0, 20, 0)
                                            .Foreground(Brushes.White)
                                            .Background(StaticResources.Brushes.AccentButtonBrush)
                                            .OnClick(_ => state.Export()),
                                        new Button()
                                            .Classes("btn")
                                            .Content("Cancel")
                                            .Width(110)
                                            .Command(state.ViewCommands.HideExportDialogCommand),
                                        new Button()
                                            .Classes("btn")
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                            .Width(110)
                                            .Margin(0, 0, 20, 0)
                                            .IsVisible(state.CanShare)
                                            .Content(new StackPanel().Orientation(Orientation.Horizontal).Children(
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                                    .Margin(new Thickness(0, 0, 8, 0))
                                                    .Text("\xE72D"),
                                                new TextBlock().Text("Share"))
                                            )
                                            .OnClick(_ => state.Share())
                                    )
                            )
                    ));

    public sealed partial class State : ObservableObject
    {
        private readonly IExportService _exportService;
        private readonly IPlatformStuffService _platformStuffService;
        private readonly AppState _appState;
        private readonly ViewCommands _viewCommands;
        private IExporter? _configuredExporter;

        [ObservableProperty]
        public partial double Scale { get; set; } = 1;

        [ObservableProperty]
        public partial ExporterInfo? SelectedExporterInfo { get; set; }

        [ObservableProperty]
        public partial Control? ExporterSettingsContent { get; set; }

        public State(IExportService exportService, IPlatformStuffService platformStuffService, AppState appState, ICommandService commandService)
        {
            _exportService = exportService;
            _platformStuffService = platformStuffService;
            _appState = appState;
            _viewCommands = commandService.GetCommandList<ViewCommands>()!;

            if (Exporters.Count > 0)
                SelectedExporterInfo = Exporters.First();

            _appState.UiState.WatchFor(x => x.ShowExportDialog, () =>
            {
                if (_appState.UiState.ShowExportDialog)
                    UpdatePreview();
            });

            _appState.UiState.WatchFor(x => x.PreferredExportFormat, () => SelectExporter(_appState.UiState.PreferredExportFormat));
        }

        public ViewCommands ViewCommands => _viewCommands;

        public SKBitmapObservable Preview { get; } = new();

        public IReadOnlyList<ExporterInfo> Exporters => _exportService.RegisteredExporters;

        public bool CanShare => _platformStuffService.CanShare;

        partial void OnScaleChanged(double value)
        {
            UpdatePreview();
        }

        partial void OnSelectedExporterInfoChanged(ExporterInfo? value)
        {
            UpdateSettingsControl(value);
            UpdatePreview();
        }

        public async void Export()
        {
            if (SelectedExporterInfo == null)
                return;

            try
            {
                using var uiBlocker = new UiBlocker("Exporting...");
                Logger.LogEventWithParams("Exporting image", new Dictionary<string, string?>
                {
                    { "Exporter", SelectedExporterInfo.Name }
                });

                var nodesToExport = _exportService.GetNodesToExport(Scale);

                if (_configuredExporter != null)
                {
                    await _exportService.ExportNodesAsync(nodesToExport, Scale, _configuredExporter);
                }
                else
                {
                    await _exportService.ExportNodesAsync(nodesToExport, Scale, SelectedExporterInfo);
                }

                _viewCommands.HideExportDialogCommand.Execute();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void Share()
        {
            var exporter = SelectedExporterInfo ?? Exporters.FirstOrDefault();
            if (exporter == null)
            {
                Logger.Log("Could not find suitable exporter.");
                return;
            }

            if (exporter.CreateInstanceFunc() is not IStreamExporter instance)
            {
                Logger.Log("This exporter can not be used to share");
                return;
            }

            _platformStuffService.Share(instance, Scale);
            _viewCommands.HideExportDialogCommand.Execute();
        }

        private void UpdateSettingsControl(ExporterInfo? exporterInfo)
        {
            if (exporterInfo == null)
            {
                _configuredExporter = null;
                ExporterSettingsContent = null;
                return;
            }

            _configuredExporter = exporterInfo.CreateInstanceFunc();

            if (exporterInfo.ExporterType == typeof(SpriteSheetExporter))
            {
                var settingsView = ViewFactory.Create<SpriteSheetExportSettingsView>();
                settingsView.Exporter = (SpriteSheetExporter)_configuredExporter;
                ExporterSettingsContent = settingsView;
            }
            else if (exporterInfo.ExporterType == typeof(SpritePngSequenceExporter))
            {
                var settingsView = ViewFactory.Create<SpritePngSequenceExporterSettingsView>();
                settingsView.Exporter = (SpritePngSequenceExporter)_configuredExporter;
                ExporterSettingsContent = settingsView;
            }
            else
            {
                _configuredExporter = null;
                ExporterSettingsContent = null;
            }
        }

        private void UpdatePreview()
        {
            if (_appState.CurrentProject.CurrentNodeEditor is not SpriteEditor spriteEditor)
                return;

            var nodesToExport = _exportService.GetNodesToExport(Scale);
            var preview = nodesToExport.ToArray()
                .RenderToBitmap(
                    spriteEditor.CurrentSprite.UseBackgroundColor
                        ? spriteEditor.CurrentSprite.BackgroundColor
                        : SKColor.Empty,
                    Scale);

            Preview.SetBitmap(preview);
        }

        private void SelectExporter(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return;

            var selected = Exporters.FirstOrDefault(x =>
                string.Equals(x.Id, format, StringComparison.OrdinalIgnoreCase)
                || x.CreateInstanceFunc().SupportedExtensions.Any(ext => string.Equals(ext, format, StringComparison.OrdinalIgnoreCase)));
            if (selected != null)
            {
                SelectedExporterInfo = selected;
            }
        }
    }
}