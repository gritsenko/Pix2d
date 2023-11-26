using Avalonia.Controls.Shapes;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar;

public class ToolItemGroupView : ComponentBase
{
    protected override object Build() =>
        new Grid().Classes("toolbar-button-container").Rows("auto").Cols("auto")
            // Add tooltip here and not to the button because using `DataTemplates` somehow breaks the
            // tooltip content.
            .ToolTip(() => ActiveItem?.ToolState.ToolTip)
            .Children(
                new Border()
                    .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .IsVisible(() => ActiveItem?.IsSelected ?? false),
                new ContentControl()
                    .Content(() => ActiveItem),
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

    public string GroupName { get; set; }

    public List<ToolItemView> Items { get; set; } = new();

    public ToolItemView? ActiveItem { get; private set; }

    public void SetActiveItem(ToolItemView item)
    {
        ActiveItem = item;
        StateHasChanged();
    }
}