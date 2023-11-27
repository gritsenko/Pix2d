using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolItemGroupView : ComponentBase
{
    protected override object Build() =>
        new Grid().Classes("toolbar-button-container").Rows("auto").Cols("auto")
            // Add tooltip here and not to the button because using `DataTemplates` somehow breaks the
            // tooltip content.
            .Ref(out _gridContainer)
            .ToolTip(ActiveItem?.ToolTip)
            .Children(
                new Border()
                    .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .IsVisible(() => IsSelected),
                new Button()
                    .Classes("toolbar-button")
                    .OnClick(OnButtonClicked)
                    .Content(() => ActiveToolIconKey)
                    .IsEnabled(() => !AppState.CurrentProject.IsAnimationPlaying || ActiveItem.EnabledDuringAnimation)
                    .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector),
                new Path()
                    .Data(Geometry.Parse("F1 M 4,0L 4,4L 0,4"))
                    .Fill(Color.Parse("#FFCCCCCC").ToBrush())
                    .Width(8)
                    .Height(8)
                    .Stretch(Stretch.Fill)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .IsVisible(() => true)
            );

    public Grid _gridContainer;

    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] IToolService ToolService { get; set; } = null!;

    public bool IsSelected { get; set; }
    public string GroupName { get; set; }

    public ToolState? ActiveItem { get; private set; }
    public string ActiveToolIconKey => ActiveItem?.IconKey ?? "";
    protected override void OnAfterInitialized()
    {
        AppState.ToolsState.WatchFor(x => x.CurrentToolKey, OnToolChanged);
    }

    private void OnButtonClicked(RoutedEventArgs obj)
    {
        AppState.ToolsState.ActiveToolGroup = GroupName;
        AppState.UiState.ShowToolProperties = false;

        if (IsSelected)
        {
            AppState.UiState.ShowToolGroup = !AppState.UiState.ShowToolGroup;
        }
        else
        {
            AppState.UiState.ShowToolGroup = false;
            ToolService.ActivateTool(ActiveItem.Name);
        }
        
        this.StateHasChanged();
    }

    private void OnToolChanged()
    {
        IsSelected = false;
        var activeTool = AppState.ToolsState.CurrentTool;
        if (activeTool.GroupName == GroupName)
        {
            IsSelected = true;
            SetActiveItem(activeTool);
        }
        else
        {
            StateHasChanged();
        }
    }

    public void SetActiveItem(ToolState item)
    {
        ActiveItem = item;
        _gridContainer.ToolTip(item?.ToolTip);
        StateHasChanged();
    }
}