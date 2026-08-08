namespace SkiaNodes.Interactive;

/// <summary>
/// Cursor an interactive node asks the host control for while the pointer is over it. Deliberately a tiny
/// platform-neutral enum: SkiaNodes knows nothing about Avalonia, so the host (Pix2d's <c>SkiaCanvas</c>)
/// maps these onto real cursors. Extend it when a node needs a shape that isn't here — not with a
/// platform cursor type.
/// </summary>
public enum SKCursorType
{
    /// <summary>Leave the cursor to the host — the tool's own cursor, in practice.</summary>
    Default,

    /// <summary>"You can grab this": draggable on-canvas handles.</summary>
    Hand
}
