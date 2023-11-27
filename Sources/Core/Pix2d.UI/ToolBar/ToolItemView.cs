using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolItemView : ComponentBase
{
    private ToolState _toolState;

    public ToolItemView(ToolState toolState)
    {
        _toolState = toolState;
        Initialize();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, StateHasChanged);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppState.CurrentProject.Unwatch(x => x.IsAnimationPlaying, StateHasChanged);
    }

    protected override object Build() =>
        new Grid().Classes("toolbar-button-container").Rows("auto").Cols("auto")
            // Add tooltip here and not to the button because using `DataTemplates` somehow breaks the
            // tooltip content.
            .ToolTip(_toolState?.ToolTip)
            .Children(
            new Border()
                .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                .Background(StaticResources.Brushes.SelectedItemBrush)
                .IsVisible(() => IsSelected),
            new Button()
                .Classes("toolbar-button")
                .OnClick(OnButtonClicked)
                .Content(() => ToolIconKey)
                .IsEnabled(() => !AppState.CurrentProject.IsAnimationPlaying || ToolState.EnabledDuringAnimation)
                .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector),
            new Path()
                .Data(Geometry.Parse("F1 M 4,0L 4,4L 0,4"))
                .Fill(Color.Parse("#FFCCCCCC").ToBrush())
                .Width(8)
                .Height(8)
                .Stretch(Stretch.Fill)
                .VerticalAlignment(VerticalAlignment.Bottom)
                .HorizontalAlignment(HorizontalAlignment.Right)
                .IsVisible(() => ShowProperties)
        );

    [Inject] IToolService ToolService { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    public string ToolKey => ToolState?.Name ?? "";

    public string ToolIconKey => ToolState?.IconKey ?? "";
    public bool IsSelected => AppState.ToolsState.CurrentToolKey == ToolKey;

    public bool ShowProperties => ToolState?.HasToolProperties ?? false;
    public ToolState ToolState
    {
        get => _toolState; 
        set
        {
            _toolState = value;
            StateHasChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
    }

    private void OnButtonClicked(RoutedEventArgs args)
    {
        AppState.UiState.ShowToolGroup = false;

        if (IsSelected)
        {
            if(ToolState.HasToolProperties)
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