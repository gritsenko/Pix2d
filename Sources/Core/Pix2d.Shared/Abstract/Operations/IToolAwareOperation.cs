namespace Pix2d.Abstract.Operations;

/// <summary>
/// Operation that knows which tool was active when it was created. The <see cref="Services.IOperationService"/>
/// uses this to restore the right tool after <c>Undo</c>/<c>Redo</c> so the UI state always matches the
/// drawing-layer state — important for selection/transform flows where undoing across a tool switch would
/// otherwise leave the UI in an inconsistent state.
/// </summary>
public interface IToolAwareOperation : IEditOperation
{
    /// <summary>
    /// Tool key (matches <c>ToolState.Name</c> / <c>Type.Name</c>) that was active immediately BEFORE this
    /// operation was created — restored on <c>Undo</c>. Null means "don't change the tool".
    /// </summary>
    string? ToolKeyBeforeOperation { get; }

    /// <summary>
    /// Tool key that should be active immediately AFTER this operation completes — used by <c>Redo</c> and
    /// by the initial <c>Perform</c> path when the operation is re-played. Null means "don't change the tool".
    /// </summary>
    string? ToolKeyAfterOperation { get; }
}
