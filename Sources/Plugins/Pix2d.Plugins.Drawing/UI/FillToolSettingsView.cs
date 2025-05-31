using Avalonia.Data;
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
                    .IsChecked(EraseMode, BindingMode.TwoWay)
            );

    public bool EraseMode
    {
        get => ((FillTool)DataContext)?.EraseMode ?? false;
        set => ((FillTool)DataContext).EraseMode = value;
    }
}