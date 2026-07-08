using Avalonia.Interactivity;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public partial class ToolItemView : ViewBase<ToolItemView.State>
{
    public ToolItemView(ToolState toolState, IToolService toolService, AppState appState)
        : base(new State(toolState, toolService, appState))
    {
    }

    protected override object Build(State state) =>
        new Button()
            .Ref(out _button)
            .Classes("toolbar-button")
            .OnClick(OnButtonClicked)
            .IsEnabled(state, x => x.IsEnabled)
            .ToolTip_Tip(state.ToolTipText)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .ClipToBounds(true)
            .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
            .Content(
                new Border()
                    .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
                    .ClipToBounds(true)
                    .Child(
                        new Grid()
                            .Children(
                                new ContentControl()
                                    .Name("tool-item-border")
                                    .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                                    .Content(state, x => x.ToolIconKey)
                            )
                    )
            );

    private Button _button = null!;

    public ToolState ToolState => ViewModel!.ToolState;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ViewModel!.PropertyChanged += OnStatePropertyChanged;
        UpdateSelectedClass(ViewModel.IsSelected);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (ViewModel != null)
            ViewModel.PropertyChanged -= OnStatePropertyChanged;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(State.IsSelected))
            UpdateSelectedClass(ViewModel!.IsSelected);
    }

    private void UpdateSelectedClass(bool isSelected)
    {
        if (_button == null)
            return;

        if (isSelected)
            _button.Classes.Add("selected");
        else
            _button.Classes.Remove("selected");
    }

    private void OnButtonClicked(RoutedEventArgs args)
    {
        ViewModel!.HandleClick();
        UpdateSelectedClass(ViewModel.IsSelected);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IToolService _toolService;

        public State(ToolState toolState, IToolService toolService, AppState appState)
        {
            _appState = appState;
            _toolService = toolService;
            ToolState = toolState;
            ToolIconKey = toolState.IconKey ?? string.Empty;
            ToolTipText = L(toolState.ToolTip ?? string.Empty);

            SyncFromAppState();

            _appState.SpriteEditorState.WatchFor(x => x.IsPlayingAnimation, SyncFromAppState);
            _appState.ToolsState.WatchFor(x => x.CurrentToolKey, SyncFromAppState);
            _appState.ToolsState.WatchFor(x => x.IsColorPickerModeActive, SyncFromAppState);
        }

        public ToolState ToolState { get; }

        public string ToolKey => ToolState.Name;

        [ObservableProperty]
        public partial string ToolIconKey { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ToolTipText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        public void HandleClick()
        {
            _appState.UiState.ShowToolGroup = false;

            // Decide on the *actual* active tool, not IsSelected — the latter also lights up the eyedropper
            // during transient Alt-pick mode (#184), so clicking it then must still activate it.
            var isActiveTool = _appState.ToolsState.CurrentToolKey == ToolKey;
            if (isActiveTool)
            {
                if (ToolState.HasToolProperties)
                    _appState.UiState.ShowToolProperties = !_appState.UiState.ShowToolProperties;
            }
            else
            {
                _appState.UiState.ShowToolProperties = false;
                _toolService.ActivateTool(ToolState.ToolType);
            }

            SyncFromAppState();
        }

        private void SyncFromAppState()
        {
            // The eyedropper item also lights up while a brush-family tool is in transient Alt-pick mode (#184).
            IsSelected = _appState.ToolsState.CurrentToolKey == ToolKey
                         || (_appState.ToolsState.IsColorPickerModeActive && ToolKey == nameof(EyedropperTool));
            IsEnabled = !_appState.SpriteEditorState.IsPlayingAnimation || ToolState.EnabledDuringAnimation;
        }
    }
}