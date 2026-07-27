#nullable enable
using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public static class DrawingTargetExtensions
{
    private static readonly int BytesPerPixel = new SKImageInfo(1, 1, Pix2DAppSettings.ColorType).BytesPerPixel;

    /// <summary>
    /// Restores a raw pixel snapshot that an undoable operation captured earlier, skipping the write when
    /// the target has been resized since — instead of letting <c>BitmapNode.SetData</c> throw
    /// <c>"Size of input data … is not equal to the size of the bitmap …"</c>.
    /// <para>
    /// Every selection operation keeps a full <c>byte[]</c> of its drawing target and pushes it back on
    /// undo/redo, while a crop/resize in between changes how many bytes that target accepts. Throwing
    /// there aborts the undo *after* the operation has been popped from the history but before it reaches
    /// the redo stack, so the history desyncs and the user just sees the same error again on the next
    /// undo. A skipped pixel restore keeps the history walkable — the geometry belongs to the
    /// crop/resize operation, which restores it (and its own pixels) when the undo reaches it.
    /// </para>
    /// <para>
    /// Same contract as the run-length guard in <c>DrawingOperationWithDiffState.ApplyChanges</c>:
    /// mismatched history payloads are skipped and traced, never applied to a buffer they don't fit.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> when the snapshot was applied, <c>false</c> when it was skipped as incompatible.</returns>
    public static bool TryRestoreData(this IDrawingTarget target, byte[] data, string operationName)
    {
        // An empty buffer means "clear the bitmap" and carries no size expectation (BitmapNode.SetData).
        if (data.Length > 0)
        {
            var size = target.GetSize();
            var expectedLength = (int)size.Width * (int)size.Height * BytesPerPixel;
            if (expectedLength > 0 && data.Length != expectedLength)
            {
                Logger.Trace($"Skipping incompatible {operationName} pixel restore: snapshot has {data.Length} bytes"
                             + $" but the target is now {(int)size.Width}x{(int)size.Height} ({expectedLength} bytes).");
                return false;
            }
        }

        target.SetData(data);
        return true;
    }
}
