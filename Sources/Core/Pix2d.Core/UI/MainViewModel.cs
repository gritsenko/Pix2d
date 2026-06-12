using System.Linq;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Import.Flow;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Command;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.UI.Styles;
using SkiaSharp;

namespace Pix2d.UI;
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly IDialogService _dialogService;
    private readonly IMessenger _messenger;
    private readonly IImportFlowService _importFlowService;
    private bool _isSyncing;

    [ObservableProperty]
    public partial bool ShowRatePrompt { get; set; }

    [ObservableProperty]
    public partial bool ShowExtraTools { get; set; }

    [ObservableProperty]
    public partial bool ShowTimeline { get; set; }

    [ObservableProperty]
    public partial bool ShowLayers { get; set; }

    [ObservableProperty]
    public partial bool ShowToolGroup { get; set; }

    [ObservableProperty]
    public partial bool ShowExportDialog { get; set; }

    [ObservableProperty]
    public partial bool ShowMenu { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool ShowColorEditor { get; set; }

    [ObservableProperty]
    public partial bool ShowBrushSettings { get; set; }

    [ObservableProperty]
    public partial bool ShowPreviewPanel { get; set; }

    [ObservableProperty]
    public partial bool ShowCanvasResizePanel { get; set; }

    [ObservableProperty]
    public partial bool ShowLayerProperties { get; set; }

    public MainViewModel(
        AppState appState,
        IDialogService dialogService,
        IMessenger messenger,
        ICommandService commandService,
        IImportFlowService importFlowService)
    {
        _appState = appState;
        _dialogService = dialogService;
        _messenger = messenger;
        _importFlowService = importFlowService;

        ViewCommands = commandService.GetCommandList<ViewCommands>()!;

        SyncFromAppState();

        _appState.UiState.Watch(SyncFromAppState);
        _appState.WatchFor(x => x.IsBusy, SyncFromAppState);
    }

    public ViewCommands ViewCommands { get; }

    public double UiScale => _appState.UiScale;

    public void InitializePanelsContainer(Canvas panelsContainer)
    {
        _dialogService.SetPanelsContainer(panelsContainer);
    }

    public void NotifyWindowClicked(StyledElement element)
    {
        _messenger.Send(new WindowClickedMessage(element));
    }

    public void UpdateResponsiveLayout(double width)
    {
        var visualState = width <= 500 ? nameof(VisualStates.Narrow) : nameof(VisualStates.Wide);
        if (_appState.UiState.VisualState != visualState)
            _appState.UiState.VisualState = visualState;
    }

    public async Task HandleDropAsync(IReadOnlyList<IStorageItem> droppedFiles, SKPoint? dropWorldPosition)
    {
        // Collect the whole drop into one batch so animation grouping works across multiple files.
        var files = droppedFiles
            .OfType<IStorageFile>()
            .Select(f => (IFileContentSource)new NetFileSource(System.Net.WebUtility.UrlDecode(f.Path.AbsolutePath)))
            .ToList();

        if (files.Count == 0)
            return;

        await _importFlowService.RunImportFlowAsync(new ImportRequest(files, dropWorldPosition, FromDrag: true));
    }

    partial void OnShowColorEditorChanged(bool value)
    {
        if (_isSyncing)
            return;

        _appState.UiState.ShowColorEditor = value;
    }

    partial void OnShowBrushSettingsChanged(bool value)
    {
        if (_isSyncing)
            return;

        _appState.UiState.ShowBrushSettings = value;
    }

    partial void OnShowPreviewPanelChanged(bool value)
    {
        if (_isSyncing)
            return;

        _appState.UiState.ShowPreviewPanel = value;
    }

    partial void OnShowCanvasResizePanelChanged(bool value)
    {
        if (_isSyncing)
            return;

        _appState.UiState.ShowCanvasResizePanel = value;
    }

    partial void OnShowLayerPropertiesChanged(bool value)
    {
        if (_isSyncing)
            return;

        _appState.UiState.ShowLayerProperties = value;
    }

    private void SyncFromAppState()
    {
        _isSyncing = true;

        ShowRatePrompt = _appState.UiState.ShowRatePrompt;
        ShowExtraTools = _appState.UiState.ShowExtraTools;
        ShowTimeline = _appState.UiState.ShowTimeline;
        ShowLayers = _appState.UiState.ShowLayers;
        ShowToolGroup = _appState.UiState.ShowToolGroup;
        ShowExportDialog = _appState.UiState.ShowExportDialog;
        ShowMenu = _appState.UiState.ShowMenu;
        ShowColorEditor = _appState.UiState.ShowColorEditor;
        ShowBrushSettings = _appState.UiState.ShowBrushSettings;
        ShowPreviewPanel = _appState.UiState.ShowPreviewPanel;
        ShowCanvasResizePanel = _appState.UiState.ShowCanvasResizePanel;
        ShowLayerProperties = _appState.UiState.ShowLayerProperties;
        IsBusy = _appState.IsBusy;

        _isSyncing = false;
    }
}