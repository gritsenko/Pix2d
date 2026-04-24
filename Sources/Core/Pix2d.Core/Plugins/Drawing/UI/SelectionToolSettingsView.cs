namespace Pix2d.Plugins.Drawing.UI;

public class SelectionToolSettingsView : ViewBase
{
    protected override object Build() =>
    ViewFactory.Create<ClipboardActionsView>();
}