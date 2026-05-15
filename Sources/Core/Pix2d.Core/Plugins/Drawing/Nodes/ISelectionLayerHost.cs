using Pix2d.Abstract.Drawing;
using Pix2d.Services;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// Narrow seam between <see cref="DrawingLayerNode"/> and <see cref="SelectionController"/>. The
/// controller talks to the node only through this contract — anything not listed here is
/// intentionally inaccessible. Ownership of the three bitmaps (background/working/swap) stays on
/// the node; the controller reads/writes them via these accessors.
/// </summary>
internal interface ISelectionLayerHost
{
    IDrawingTarget? DrawingTarget { get; }
    SKSize Size { get; }
    SKMatrix GetGlobalTransform();

    SKBitmap WorkingBitmap { get; }
    bool UseSwapBitmap { get; set; }
    SKBitmap? BackgroundBitmap { get; set; }

    DrawingLayerState State { get; set; }
    float Opacity { get; set; }

    void ClearWorkingBuffers();
    void ClearWorkingAndSwapBitmaps();
    void SwapWorkingBitmap();
    void ApplyWorkingBitmap();

    bool LockTransparentPixels { get; }

    void RequestRefresh();

    bool IsInBounds(SKPointI p);
    void SetPixel(int x, int y, SKColor color);

    IAspectSnapper? AspectSnapper { get; }
    Func<string?>? ActiveToolKeyProvider { get; }

    void RaiseDrawingApplied(bool saveToUndo);
}
