using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.ToolBar;

public class ToolItemView : LocalizedComponentBase
{
    private ToolState _toolState;
    
    public ToolItemView(ToolState toolState)
    {
        _toolState = toolState;
        Initialize();
    }
    
    protected override object Build() =>
        new Button()
            .Ref(out _button)
            .ToolTip(L(_toolState?.ToolTip)())
            .Classes("toolbar-button")
            // todo: update BindClass in avalonia markup with expression binding
            //.BindClass(IsSelected, "selected", bindingSource: this)
            .OnClick(OnButtonClicked)
            .IsEnabled(() => !AppState.CurrentProject.IsAnimationPlaying || ToolState.EnabledDuringAnimation)
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
                                    .Content(() => ToolIconKey)
                            )
                    )
            );

    [Inject] private IToolService ToolService { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;

    private Button _button;

    public string ToolKey => ToolState?.Name ?? "";

    public string ToolIconKey => ToolState?.IconKey ?? "";
    public bool IsSelected => AppState.ToolsState.CurrentToolKey == ToolKey;

    public ToolState ToolState
    {
        get => _toolState;
        set
        {
            _toolState = value;
            StateHasChanged();
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, StateHasChanged);
        AppState.ToolsState.WatchFor(x => x.CurrentToolKey, UpdateIsSelected);

        UpdateIsSelected();
    }

    private void UpdateIsSelected()
    {
        if (IsSelected)
            _button.Classes.Add("selected");
        else
            _button.Classes.Remove("selected");

        StateHasChanged();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppState.CurrentProject.Unwatch(x => x.IsAnimationPlaying, StateHasChanged);
        AppState.ToolsState.Unwatch(x => x.CurrentToolKey, UpdateIsSelected);
    }

    private void OnButtonClicked(RoutedEventArgs args)
    {
        AppState.UiState.ShowToolGroup = false;

        if (IsSelected)
        {
            if (ToolState.HasToolProperties)
                AppState.UiState.ShowToolProperties = !AppState.UiState.ShowToolProperties;
        }
        else
        {
            AppState.UiState.ShowToolProperties = false;
            ToolService.ActivateTool(this._toolState.Name);
        }

        this.StateHasChanged();
    }
}