using Pix2d.Plugins.Drawing.Tools;

namespace Pix2d.Plugins.Drawing.UI;

public class FillToolSettingsView: ComponentBase
{
    protected override object Build() =>
        new StackPanel()
            .Margin(8)
            .Children(
                new ToggleSwitch()
                    .OnContent("Erase mode: On")
                    .OffContent("Erase mode: Off")
                    .IsChecked(() => EraseMode, v => EraseMode = v ?? false)
            );

    public bool EraseMode
    {
        get
        {
            if (DataContext is not FillTool fillTool)
                return false;
            return fillTool.EraseMode;
        }
        set
        {
            if (DataContext is FillTool fillTool)
                fillTool.EraseMode = value;
        }
    }
}