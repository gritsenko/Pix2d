using Pix2d.Abstract.Operations;

namespace Pix2d.Abstract.Services;

public interface IOperationService
{

    bool CanRedo { get; }
    bool CanUndo { get; }
    int UndoOperationsCount { get; }

    //event EventHandler<OperationInvokeEventArgs> OperationInvoked;

    /// <summary>
    /// Pushes operation (or several of them) into operations stack
    /// if passed more then one operation, they will be merged into one and will be executed as one
    ///
    /// if operation already persist in stack - it won't be pushed
    /// </summary>
    /// <param name="operations">Edit operation(s)</param>
    void PushOperations(params IEditOperation[] operations);
    void InvokeAndPushOperations(params IEditOperation[] operations);

    void Undo();
    void Redo();
    void Clear();

    /// <summary>
    /// Switches the active undo/redo history to the one keyed by <paramref name="projectId"/>,
    /// creating an empty history on first use. Each open project (tab) owns its own history.
    /// </summary>
    void SetActiveHistory(Guid projectId);

    /// <summary>
    /// Drops the history of a closed project, disposing its operations.
    /// </summary>
    void RemoveHistory(Guid projectId);
}