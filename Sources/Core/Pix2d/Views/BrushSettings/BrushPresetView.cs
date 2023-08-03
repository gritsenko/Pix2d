namespace Pix2d.Views.BrushSettings;

public class BrushPresetView : ViewBase<Primitives.Drawing.BrushSettings>
{
    protected override object Build(Primitives.Drawing.BrushSettings vm) =>
        new Grid()
            .Rows("Auto,*")
            .Background(StaticResources.Brushes.InnerPanelBackgroundBrush)
            .Width(32)
            .Height(32)
            .Children(
                new BrushItemView()
                    .Width(18)
                    .Height(18)
                    .Preset(vm),
                new TextBlock()
                    .Row(1)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .FontSize(10)
                    .Text($"{(int)vm.Scale}px")
            );

    public BrushPresetView(Primitives.Drawing.BrushSettings viewModel) : base(viewModel)
    {
    }
}