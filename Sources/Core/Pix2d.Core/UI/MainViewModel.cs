using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Import;
using Pix2d.Command;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.UI.Styles;

namespace Pix2d.UI;
public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppState _appState;
    private readonly IDialogService _dialogService;
    private readonly IMessenger _messenger;
    private readonly IProjectService _projectService;
    private readonly IImportService _importService;
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
        IProjectService projectService,
        IImportService importService,
        ICommandService commandService)
    {
        _appState = appState;
        _dialogService = dialogService;
        _messenger = messenger;
        _projectService = projectService;
        _importService = importService;

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

    public async Task HandleDropAsync(IReadOnlyList<IStorageItem> droppedFiles)
    {
        foreach (var storageFile in droppedFiles.OfType<IStorageFile>())
        {
            var path = System.Net.WebUtility.UrlDecode(storageFile.Path.AbsolutePath);
            var fileSource = new NetFileSource(path);

            if (path.EndsWith(".pxm") || path.EndsWith(".pix2d"))
            {
                await _projectService.OpenFilesAsync([fileSource]);
                return;
            }

            if (_appState.CurrentProject.CurrentNodeEditor is not IImportTarget importTarget)
                throw new ArgumentException("Import target is required");

            await _importService.ImportAsync([fileSource], importTarget);
        }
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