using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Tools;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public partial class ToolItemGroupView : ViewBase<ToolItemGroupView.State>
{
    public ToolItemGroupView(AppState appState, IToolService toolService)
        : base(new State(appState, toolService))
    {
    }

    protected override object Build(State state) =>
        new Button()
            .Ref(out _button)
            .Classes("toolbar-button")
            .OnClick(OnButtonClicked)
            .ClipToBounds(true)
            .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .IsEnabled(state, x => x.IsEnabled)
            .Content(
                new Border()
                    .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
                    .ClipToBounds(true)
                    .Child(
                        new Grid()
                            .Ref(out _gridContainer)
                            .Children(
                                new ContentControl()
                                    .Name("tool-item-border")
                                    .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                                    .Content(state, x => x.ActiveToolIconKey),
                                new Rectangle()
                                    .RadiusX(StaticResources.Measures.PipkaCornerRadius)
                                    .RadiusY(StaticResources.Measures.PipkaCornerRadius)
                                    .Fill(Colors.White.WithAlpha(0.3f).ToBrush())
                                    .Width(8)
                                    .Height(8)
                                    .Stretch(Stretch.Fill)
                                    .VerticalAlignment(VerticalAlignment.Bottom)
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                            )
                    )
            );

    private Button _button = null!;
    private Grid _gridContainer = null!;

    public string GroupName
    {
        get => ViewModel?.GroupName ?? string.Empty;
        set
        {
            ViewModel!.GroupName = value;
            ViewModel.SyncFromAppState();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        ViewModel!.PropertyChanged += OnStatePropertyChanged;
        UpdateSelectedClass(ViewModel.IsSelected);
        UpdateToolTip(ViewModel.ActiveToolTipText);
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

        if (e.PropertyName == nameof(State.ActiveToolTipText))
            UpdateToolTip(ViewModel!.ActiveToolTipText);
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

    private void UpdateToolTip(string toolTip)
    {
        if (_gridContainer != null)
            _gridContainer.ToolTip_Tip(toolTip);
    }

    private void OnButtonClicked(RoutedEventArgs obj)
    {
        ViewModel!.HandleClick();
    }

    public void SetActiveItem(ToolState item)
    {
        ViewModel!.SetActiveItem(item);
        UpdateToolTip(ViewModel.ActiveToolTipText);
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IToolService _toolService;

        public State(AppState appState, IToolService toolService)
        {
            _appState = appState;
            _toolService = toolService;

            _appState.ToolsState.WatchFor(x => x.CurrentToolKey, SyncFromAppState);
            _appState.SpriteEditorState.WatchFor(x => x.IsPlayingAnimation, SyncFromAppState);
        }

        [ObservableProperty]
        public partial string GroupName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        [ObservableProperty]
        public partial string ActiveToolIconKey { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ActiveToolTipText { get; set; } = string.Empty;

        public ToolState? ActiveItem { get; private set; }

        public void HandleClick()
        {
            _appState.ToolsState.ActiveToolGroup = GroupName;
            _appState.UiState.ShowToolProperties = false;

            if (IsSelected)
            {
                _appState.UiState.ShowToolGroup = !_appState.UiState.ShowToolGroup;
            }
            else
            {
                _appState.UiState.ShowToolGroup = false;

                if (ActiveItem != null)
                    _toolService.ActivateTool(ActiveItem.ToolType);
            }

            SyncFromAppState();
        }

        public void SetActiveItem(ToolState item)
        {
            ActiveItem = item;
            ActiveToolIconKey = item.IconKey ?? string.Empty;
            ActiveToolTipText = L(item.ToolTip ?? string.Empty);
            UpdateEnabledState();
        }

        public void SyncFromAppState()
        {
            var activeTool = _appState.ToolsState.CurrentTool;
            IsSelected = activeTool?.GroupName == GroupName;

            if (IsSelected && activeTool != null && !ReferenceEquals(ActiveItem, activeTool))
            {
                ActiveItem = activeTool;
                ActiveToolIconKey = activeTool.IconKey ?? string.Empty;
                ActiveToolTipText = L(activeTool.ToolTip ?? string.Empty);
            }

            UpdateEnabledState();
        }

        private void UpdateEnabledState()
        {
            IsEnabled = !_appState.SpriteEditorState.IsPlayingAnimation || (ActiveItem?.EnabledDuringAnimation == true);
        }
    }
}