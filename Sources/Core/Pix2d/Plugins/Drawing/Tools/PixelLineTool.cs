using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Line",
    Group = "Shapes"
)]
public class PixelLineTool : PixelShapeTool<LineShapeBuilder>
{
}