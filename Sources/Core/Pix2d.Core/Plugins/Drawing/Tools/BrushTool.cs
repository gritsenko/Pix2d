using Pix2d.Abstract.Tools;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = false,
    EnabledDuringAnimation = true,
    DisplayName = "Brush tool",
    HotKey = "B"
)]
public class BrushTool(IMessenger messenger, IDrawingService drawingService, ISelectionService selectionService)
    : PixelBrushToolBase(messenger, drawingService, selectionService);