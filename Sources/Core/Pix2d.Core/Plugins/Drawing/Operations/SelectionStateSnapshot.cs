using Pix2d.Plugins.Drawing.Nodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

/// <summary>
/// Everything needed to put one pixel selection back on screen: the selection layer, the canvas underneath
/// it, and whether it was a contour-only marquee or lifted pixels. Feeds
/// <c>DrawingLayerNode.SetSelection</c>, so it is what selection operations replay on undo/redo.
/// </summary>
public sealed record SelectionStateSnapshot(SpriteSelectionNode SelectionLayer, SKBitmap BackgroundBitmap, bool ContourOnly);
