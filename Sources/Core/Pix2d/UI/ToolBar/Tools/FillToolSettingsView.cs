using Pix2d.Plugins.Drawing.Tools;
using SkiaSharp;

namespace Pix2d.UI.ToolBar.Tools;

public class FillToolSettingsView : ViewBase<FillTool>
{
    protected override object Build(FillTool vm) =>
        new StackPanel()
            .Margin(8)
            .Children(
                new ToggleSwitch()
                    .Content("Erase mode")
                    .IsChecked(vm.EraseMode, BindingMode.TwoWay, bindingSource: vm)
            );

    public FillToolSettingsView(FillTool viewModel) : base(viewModel)
    {
    }
}