using System.Collections;
using System.Diagnostics;
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Primitives.Operations;

namespace Pix2d.Services;

public class OperationService : IOperationService
{
    public AppState AppState { get; }
    public const int MaxSteps = 100;

    private readonly LimitedSizeStack<IEditOperation> _redoOperations = new(MaxSteps);

    private readonly LimitedSizeStack<IEditOperation> _undoOperations = new(MaxSteps) { OnRemoveItem = OnRemoveItemFromHistory };

    private IEditOperation _currentOperation;

    public bool CanUndo => _undoOperations.Any();
    public int UndoOperationsCount => _undoOperations.Count;
    public bool CanRedo => _redoOperations.Any();

    public event EventHandler<OperationInvokeEventArgs> OperationInvoked;

    public OperationService(AppState appState)
    {
        AppState = appState;
        Messenger.Default.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
    }

    private void OnProjectLoaded(ProjectLoadedMessage message)
    {
        Clear();
    }

    public void PushOperations(params IEditOperation[] operations)
    {
        var ops = operations.Where(x => x != null).ToArray();

        if (ops.Length == 0 || IsAlreadyPushed(ops))
            return;

        var operation = ops.Length > 1 ? new BulkEditOperation(operations) : operations[0];

        _undoOperations.Push(operation);

        ClearRedoOperations();

#if DEBUG
        System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
        var path = "\n" + string.Join(" \\ ", t.GetFrames().Take(3).Select(x => x.GetMethod().DeclaringType.Name + "." + x.GetMethod().Name).Reverse());
        Debug.WriteLine("Operation (" + _undoOperations.Count + ") pushed: " + operation.GetType() + " from:" + path);
#endif

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Perform, operation));
    }

    public void InvokeAndPushOperations(params IEditOperation[] operations)
    {
        foreach (var editOperation in operations) editOperation.OnPerform();

        PushOperations(operations);
    }

    private void ClearRedoOperations()
    {
        foreach (var operation in _redoOperations.OfType<IDisposable>()) operation.Dispose();
        _redoOperations.Clear();
    }

    //dispose items before remove from history
    private static void OnRemoveItemFromHistory(IEditOperation operation)
    {
        if (operation is IDisposable dop)
            dop.Dispose();
    }

    //todo: optimize in 2020
    internal bool IsAlreadyPushed(IEditOperation[] ops)
    {
        bool EqualOrContains(IEditOperation op1, IEditOperation op2)
        {
            return op1 == op2 || op1 is BulkEditOperation bulkOp && bulkOp.HasOperation(op2);
        }

        foreach (var op in ops)
        {
            if (EqualOrContains(_currentOperation, op))
            {
                Debug.WriteLine("Operation already pushed: " + op.GetType());
                return true;
            }

            foreach (var operation in _undoOperations)
                if (EqualOrContains(operation, op))
                {
                    Debug.WriteLine("Operation already pushed: " + op.GetType());
                    return true;
                }

            foreach (var operation in _redoOperations)
                if (EqualOrContains(operation, op))
                {
                    Debug.WriteLine("Operation already pushed: " + op.GetType());
                    return true;
                }
        }


        return false;
    }

    public void Undo()
    {
        if (_undoOperations.Count == 0) return;

        _currentOperation = _undoOperations.Pop();

        if (_currentOperation == null) return;

        _currentOperation.OnPerformUndo();
        _redoOperations.Push(_currentOperation);

        Debug.WriteLine("Operation Undo performed: " + _currentOperation.GetType());

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Undo, _currentOperation));
    }

    public void Redo()
    {
        if (_redoOperations.Count == 0) return;

        _currentOperation = _redoOperations.Pop();
        _currentOperation.OnPerform();

        _undoOperations.Push(_currentOperation);

        Debug.WriteLine("Operation Redo performed: " + _currentOperation.GetType());

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Redo, _currentOperation));
    }

    public void Clear()
    {
        _undoOperations.Clear();
        _redoOperations.Clear();

        GC.Collect();
    }


    protected virtual void OnOperationInvoked(OperationInvokeEventArgs e)
    {
        OperationInvoked?.Invoke(this, e);
        Messenger.Default.Send(new OperationInvokedMessage(e.OperationType, e.Operation));
    }

    private class LimitedSizeStack<T>(int maxSize) : IEnumerable<T>
    {
        private readonly LinkedList<T> _list = [];

        public Action<T> OnRemoveItem { get; set; }

        public void Push(T item)
        {
            lock (_list)
            {
                _list.AddFirst(item);
                if (_list.Count > maxSize)
                {
                    if (_list.Last == null)
                        throw new Exception("No items in stack!");

                    OnRemoveItem?.Invoke(_list.Last.Value);
                    _list.RemoveLast();
                }
            }
        }

        public T Pop()
        {
            if (_list.First == null)
                throw new Exception("No items in stack!");

            var item = _list.First.Value;
            _list.RemoveFirst();
            return item;
        }

        public int Count => _list.Count;

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            _list.Clear();
        }
    }
}