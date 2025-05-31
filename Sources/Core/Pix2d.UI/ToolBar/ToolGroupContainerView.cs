using System.Linq;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolGroupContainerView : ComponentBase
{
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<ToolItemView>()
            .Width(44)
            .Height(44)
            .Margin(6),

        new Style<Shape>(s => s.Class("toolbar-button").Descendant().Is<Shape>())
            .Fill(StaticResources.Brushes.ForegroundBrush.ToImmutable()),

        new Style<Shape>(s => s.Class("selected").Descendant().Is<Shape>())
            .Fill(Brushes.White.ToImmutable()),

        new Style<TextBlock>(x => x.Class("ToolIcon"))
            .Height(26)
            .Width(26)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .TextAlignment(TextAlignment.Center)
            .FontSize(22),

    ];

    protected override object Build() =>
        new Border()
            .Classes("Panel")
            .Child(
                new StackPanel()
                    .Ref(out _itemsPanel)
            );

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