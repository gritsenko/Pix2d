using System.Linq;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolGroupContainerView : ComponentBase
{
    protected override object Build() =>
        new StackPanel()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Ref(out _itemsPanel);

    [Inject] AppState AppState { get; set; } = null!;

    private StackPanel _itemsPanel;
    private string _currentGroup;

    protected override void OnAfterInitialized()
    {
        AppState.ToolsState.WatchFor(x => x.ActiveToolGroup, () => ReloadItems(AppState.ToolsState.ActiveToolGroup));
        AppState.UiState.WatchFor(x => x.ShowToolGroup, () => ReloadItems(AppState.ToolsState.ActiveToolGroup));
    }

    private void ReloadItems(string group)
    {
        if (_currentGroup == group) return;

        _currentGroup = group;
        var items = AppState.ToolsState.Tools.Where(x => x.GroupName == group);
        _itemsPanel.Children.Clear();
        foreach (var item in items)
        {
            _itemsPanel.Children.Add(new ToolItemView(item));
        }
    }
}