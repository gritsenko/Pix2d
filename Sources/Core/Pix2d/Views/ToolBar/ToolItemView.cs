using System.Windows.Input;
using Avalonia.Controls.Shapes;
using Pix2d.ViewModels.ToolBar;

namespace Pix2d.Views.ToolBar;

public class ToolItemView : ViewBase<ToolItemViewModel>
{
    public ToolItemView(ToolItemViewModel viewModel) : base(viewModel) { }

    protected override object Build(ToolItemViewModel vm) =>
        new Grid().Children(
            new Border()
                .BorderThickness(4, 0, 0, 0)
                .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                .Background(StaticResources.Brushes.SelectedItemBrush)
                .IsVisible(vm.IsSelected),

            new Button()
                .Classes("toolbar-button")
                .Command(SelectToolCommand)
                .CommandParameter(new Binding())
                .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                .Background(Colors.Transparent.ToBrush())
                .Content(vm)
                .ToolTip(vm.Tooltip),

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
}