namespace Pix2d.Primitives.Drawing;

/// <summary>
/// Lifecycle of a pixel-selection marquee on the drawing layer. Orthogonal to
/// <see cref="Plugins.Drawing.DrawingLayerState"/> which describes the active gesture/drawing.
/// </summary>
public enum SelectionPhase
{
    /// <summary>No selection exists.</summary>
    None = 0,

    /// <summary>
    /// Marquee exists in contour-edit mode: pixels are NOT lifted from the underlying canvas.
    /// Set by pixel-selection tools (Rect/Lasso/Color) — they only describe the area.
    /// </summary>
    MarqueeReady = 1,

    /// <summary>
    /// Marquee exists in full transform mode: pixels are lifted onto the selection layer and the
    /// canvas under them is cleared. Set by <c>PixelTransformTool</c> and the paste flow.
    /// </summary>
    Transforming = 2,
}
