using Pix2d.Primitives.Drawing;

namespace Pix2d.Plugins.Drawing.UI;

public class SelectionToolSettingsView : ComponentBase
{
    protected override object Build() =>
        new ClipboardActionsView();
}