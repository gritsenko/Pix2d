namespace Pix2d.Primitives.Drawing;

/// <summary>
/// How a freshly drawn marquee combines with the selection that already exists.
///
/// <para>Orthogonal to <see cref="PixelSelectionMode"/>: the latter says *how the region is described*
/// (rectangle / freeform / same colour), this says *what to do with it*. The mode is resolved per gesture
/// from the keyboard modifiers held at pointer-down (see
/// <c>PointerInputRouter.ResolveCombineMode</c>) — it is not sticky.</para>
/// </summary>
public enum SelectionCombineMode
{
    /// <summary>The new marquee replaces whatever was selected. The default, and the only mode before 3.12.</summary>
    Replace,

    /// <summary>Union — Shift.</summary>
    Add,

    /// <summary>Difference (existing minus new) — Ctrl. Alt is unavailable: it is the eyedropper modifier.</summary>
    Subtract,

    /// <summary>Intersection — Shift+Ctrl.</summary>
    Intersect
}
