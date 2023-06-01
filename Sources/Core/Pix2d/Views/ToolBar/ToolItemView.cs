using System.Windows.Input;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views.ToolBar;

public class ToolItemView : ComponentBase
{
    protected override object Build() =>
        new Grid().Children(
            new Border()
                .BorderThickness(4, 0, 0, 0)
                .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                .Background(StaticResources.Brushes.SelectedItemBrush)
                .IsVisible(IsSelected),

            new Button()
                .Classes("toolbar-button")
                .Command(SelectToolCommand)
                .CommandParameter(new Binding())
                .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                .Background(Colors.Transparent.ToBrush())
                .Content(ToolKey)
                .ToolTip(Tooltip),

            new Path()
                .Data(Geometry.Parse("F1 M 4,0L 4,4L 0,4"))
                .Fill(Color.Parse("#FFCCCCCC").ToBrush())
                .Width(8)
                .Height(8)
                .Stretch(Stretch.Fill)
                .VerticalAlignment(VerticalAlignment.Bottom)
                .HorizontalAlignment(HorizontalAlignment.Right)
                .IsVisible(new Binding("HasToolProperties"))
        );

    public ICommand? SelectToolCommand { get; set; } = null!;

    public string ToolKey { get; set; }

    public string ToolIconPath { get; } = null!;
    public bool IsSelected { get; set; }

    public string Title { get; }

    public string Tooltip { get; set; }

    //public ToolItemViewModel(string toolKey, Func<ToolSettingsBaseViewModel> settingsVmProvider)
    //{
    //    _settingsVmProvider = settingsVmProvider;
    //    ToolKey = toolKey;

    //    var tool = CoreServices.ToolService.GetToolByKey(toolKey);
    //    Title = tool.DisplayName.ToUpper();

    //    if (tool is BaseTool baseTool)
    //        ToolIconPath = baseTool.ToolIconData;

    //    Tooltip = tool.HotKey != null ? $"{tool.DisplayName} ({tool.HotKey})" : tool.DisplayName;
    //}
}