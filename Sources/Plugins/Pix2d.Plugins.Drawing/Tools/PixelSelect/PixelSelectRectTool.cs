using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.UI;
using Pix2d.Primitives.Drawing;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true,
    SettingsViewType = typeof(SelectionToolSettingsView),
    DisplayName = "Pixels select rectangle tool",
    Group = "Pixel Select",
    HotKey = "M")]
public class PixelSelectRectTool(IDrawingService drawingService, IMessenger messenger, AppState state)
    : PixelSelectToolBase(drawingService, messenger, state)
{
    public override Task Activate()
    {
        SelectionMode = PixelSelectionMode.Rectangle;
        return base.Activate();
    }
}