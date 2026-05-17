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

    /// <summary>
    /// Takes a copy-on-write snapshot of the current working bitmap and publishes it as the bitmap
    /// the compositor displays. While a snapshot is published, the live working bitmap can be written
    /// to freely without tearing — the compositor reads from the immutable snapshot instead.
    /// </summary>
    void PromoteWorkingBitmapToDisplay();

    /// <summary>
    /// Drops the current display snapshot. After this the compositor goes back to reading the live
    /// working bitmap directly. Called when the selection editor deactivates so subsequent drawing
    /// operations show the live bitmap.
    /// </summary>
    void ClearDisplaySnapshot();

    bool LockTransparentPixels { get; }

    void RequestRefresh();

    bool IsInBounds(SKPointI p);
    void SetPixel(int x, int y, SKColor color);

    IAspectSnapper? AspectSnapper { get; }
    Func<string?>? ActiveToolKeyProvider { get; }

    void RaiseDrawingApplied(bool saveToUndo);
}
