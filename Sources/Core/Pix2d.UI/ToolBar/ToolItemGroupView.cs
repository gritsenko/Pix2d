using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.Common.Extensions;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.ToolBar;

public class ToolItemGroupView : LocalizedComponentBase
{
    protected override object Build() =>
        new Button()
            .Classes("toolbar-button")
            .BindClass(IsSelected, "selected", bindingSource: this)
            .OnClick(OnButtonClicked)
            .ClipToBounds(true)
            .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .IsEnabled(() => !AppState.SpriteEditorState.IsPlayingAnimation || (ActiveItem?.EnabledDuringAnimation == true))
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
                                    .Content(() => ActiveToolIconKey),
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

    private Grid _gridContainer = null!;

    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] IToolService ToolService { get; set; } = null!;

    public bool IsSelected { get; set; }
    public string GroupName { get; set; } = "";

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
            ToolService.ActivateTool(ActiveItem!.Name);
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