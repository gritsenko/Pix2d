using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Rectangle",
    Group = "Shapes"
)]
public class PixelRectangleTool : PixelShapeTool<RectangleShapeBuilder>
{
}