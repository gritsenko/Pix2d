using System.Collections;
using System.Diagnostics;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Primitives.Operations;

namespace Pix2d.Services;

public class OperationService : IOperationService
{
    public AppState AppState { get; }
    public const int MaxSteps = 100;

    /// <summary>
    /// Undo/redo stacks of one open project. Each project (tab) owns a History; switching
    /// tabs switches the active instance via <see cref="SetActiveHistory"/>.
    /// </summary>
    private sealed class History
    {
        public readonly LimitedSizeStack<IEditOperation> RedoOperations = new(MaxSteps);
        public readonly LimitedSizeStack<IEditOperation> UndoOperations = new(MaxSteps);
        public IEditOperation? CurrentOperation;
    }

    /// <summary>
    /// Creates a project history with both stacks wired to drop an operation's disk-cached payload
    /// when it falls off the bottom (overflow eviction). The deletion is per-operation because the
    /// cache folder is shared across open tabs — see <see cref="Clear"/>.
    /// </summary>
    private History CreateHistory()
    {
        var history = new History();
        history.UndoOperations.OnRemoveItem = DiscardOperation;
        history.RedoOperations.OnRemoveItem = DiscardOperation;
        return history;
    }

    /// <summary>
    /// Drops an operation that is leaving the history for good: deletes its evicted disk payload
    /// (if any) and disposes it. Per-operation deletion is what keeps the temp operation cache from
    /// growing without bound now that <see cref="Clear"/> no longer wipes the whole shared folder
    /// while more than one project (tab) is open.
    /// </summary>
    private void DiscardOperation(IEditOperation? operation)
    {
        if (operation is ICacheableOperation cacheable)
            cacheable.ClearDiskCache(_diskCache);
        if (operation is IDisposable disposable)
            disposable.Dispose();
    }

    private readonly Dictionary<Guid, History> _histories = new();
    private Guid _activeProjectId;
    private History _active;

    private LimitedSizeStack<IEditOperation> _redoOperations => _active.RedoOperations;
    private LimitedSizeStack<IEditOperation> _undoOperations => _active.UndoOperations;

    private IEditOperation? _currentOperation
    {
        get => _active.CurrentOperation;
        set => _active.CurrentOperation = value;
    }

    // Count, not Any(): enumerating the stack now snapshots it, and these are polled by the UI.
    public bool CanUndo => _undoOperations.Count > 0;
    public int UndoOperationsCount => _undoOperations.Count;
    public bool CanRedo => _redoOperations.Count > 0;

    public event EventHandler<OperationInvokeEventArgs>? OperationInvoked;

    private readonly IOperationDiskCacheService _diskCache;
    // Lazy IToolService accessor — OperationService is registered earlier than IToolService and several
    // tools depend on IOperationService transitively, so a direct dependency would create a cycle.
    private readonly Func<IToolService>? _toolServiceProvider;
    private const int HotCacheLimit = 10;

    public OperationService(AppState appState, IOperationDiskCacheService diskCache, Func<IToolService>? toolServiceProvider = null)
    {
        AppState = appState;
        _diskCache = diskCache;
        _toolServiceProvider = toolServiceProvider;

        _activeProjectId = appState.CurrentProject.Id;
        _active = CreateHistory();
        _histories[_activeProjectId] = _active;

        Messenger.Default.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
    }

    public void SetActiveHistory(Guid projectId)
    {
        if (projectId == _activeProjectId)
            return;

        if (!_histories.TryGetValue(projectId, out var history))
        {
            history = CreateHistory();
            _histories[projectId] = history;
        }

        _activeProjectId = projectId;
        _active = history;
    }

    public void RemoveHistory(Guid projectId)
    {
        if (!_histories.Remove(projectId, out var history))
            return;

        foreach (var operation in history.UndoOperations)
            DiscardOperation(operation);
        foreach (var operation in history.RedoOperations)
            DiscardOperation(operation);
        DiscardOperation(history.CurrentOperation);

        history.UndoOperations.Clear();
        history.RedoOperations.Clear();
        history.CurrentOperation = null;

        // If the active history was removed (shouldn't happen in the normal close flow,
        // which activates a neighbor first), fall back to a fresh one for safety.
        if (projectId == _activeProjectId)
            _active = _histories[_activeProjectId] = CreateHistory();

        GC.Collect();
    }

    private void UpdateCacheStates()
    {
        RefreshCacheWindow(_undoOperations);
        RefreshCacheWindow(_redoOperations);
    }

    /// <summary>
    /// Keeps the newest <see cref="HotCacheLimit"/> operations of a stack in memory and evicts the rest to
    /// disk.
    ///
    /// <para>Those evicted payloads live in the OS temp folder, which Storage Sense, cleanmgr, a /tmp reaper
    /// or antivirus can wipe while Pix2d is running. Writes survive that — <c>OperationDiskCacheService</c>
    /// recreates its folder — but a read cannot invent the bytes back, and this runs from
    /// <see cref="PushOperations"/>, i.e. at the end of every stroke: an exception here killed the stroke
    /// that triggered it, which is the crash shape appstat saw as <c>DirectoryNotFoundException in
    /// OperationService.UpdateCacheStates</c> before the write side was fixed.</para>
    ///
    /// <para>An operation whose payload is gone can never be replayed, and neither can anything below it —
    /// a stack is applied from the top down — so the history is truncated at that point. The user keeps
    /// every step that is still intact and loses only the ones the OS deleted, instead of losing the
    /// drawing session.</para>
    /// </summary>
    private void RefreshCacheWindow(LimitedSizeStack<IEditOperation> stack)
    {
        var index = 0;

        foreach (var op in stack)
        {
            if (op is ICacheableOperation cacheable)
            {
                try
                {
                    if (index < HotCacheLimit) cacheable.RestoreFromDisk(_diskCache);
                    else cacheable.EvictToDisk(_diskCache);
                }
                catch (Exception e)
                {
                    Logger.LogException(e);
                    foreach (var dropped in stack.TruncateFrom(index))
                        DiscardOperation(dropped);
                    return;
                }
            }

            index++;
        }
    }

    private void OnProjectLoaded(ProjectLoadedMessage message)
    {
        Clear();
    }

    public void PushOperations(params IEditOperation[]? operations)
    {
        var ops = operations?.Where(x => x != null).ToArray();

        if (ops == null || ops.Length == 0 || IsAlreadyPushed(ops))
            return;

        var operation = ops.Length > 1 ? new BulkEditOperation(ops!) : ops[0]!;

        _undoOperations.Push(operation);

        ClearRedoOperations();
        UpdateCacheStates();

#if DEBUG
        System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
        var path = "\n" + string.Join(" \\ ", t.GetFrames().Take(3).Select(x => x.GetMethod()!.DeclaringType!.Name + "." + x.GetMethod()!.Name).Reverse());
        Debug.WriteLine("Operation (" + _undoOperations.Count + ") pushed: " + operation.GetType() + " from:" + path);
#endif

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Perform, operation));
    }

    public void InvokeAndPushOperations(params IEditOperation[]? operations)
    {
        if (operations == null) return;
        foreach (var editOperation in operations) editOperation?.OnPerform();

        PushOperations(operations);
    }

    private void ClearRedoOperations()
    {
        foreach (var operation in _redoOperations)
            DiscardOperation(operation);
        _redoOperations.Clear();
    }

    //todo: optimize in 2020
    internal bool IsAlreadyPushed(IEditOperation[] ops)
    {
        bool EqualOrContains(IEditOperation? op1, IEditOperation op2)
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

        RestoreToolForOperation(_currentOperation, OperationEventType.Undo);

        UpdateCacheStates();

        Debug.WriteLine("Operation Undo performed: " + _currentOperation.GetType());

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Undo, _currentOperation));
    }

    public void Redo()
    {
        if (_redoOperations.Count == 0) return;

        _currentOperation = _redoOperations.Pop();
        _currentOperation!.OnPerform();

        _undoOperations.Push(_currentOperation);

        RestoreToolForOperation(_currentOperation, OperationEventType.Redo);

        UpdateCacheStates();

        Debug.WriteLine("Operation Redo performed: " + _currentOperation.GetType());

        OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Redo, _currentOperation));
    }

    private void RestoreToolForOperation(IEditOperation operation, OperationEventType eventType)
    {
        if (_toolServiceProvider == null) return;

        var toolKey = ResolveToolKey(operation, eventType);
        if (string.IsNullOrEmpty(toolKey)) return;
        if (toolKey == AppState.ToolsState.CurrentToolKey) return;

        var toolType = AppState.ToolsState.Tools.FirstOrDefault(t => t.Name == toolKey)?.ToolType;
        if (toolType == null) return;

        try
        {
            _toolServiceProvider().ActivateTool(toolType);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Tool restoration failed for {toolKey}: {ex}");
        }
    }

    private static string? ResolveToolKey(IEditOperation operation, OperationEventType eventType)
    {
        switch (operation)
        {
            case IToolAwareOperation toolAware:
                return eventType == OperationEventType.Undo
                    ? toolAware.ToolKeyBeforeOperation
                    : toolAware.ToolKeyAfterOperation;
            case BulkEditOperation bulk:
                foreach (var inner in bulk.Operations)
                {
                    var key = ResolveToolKey(inner, eventType);
                    if (!string.IsNullOrEmpty(key))
                        return key;
                }
                return null;
            default:
                return null;
        }
    }

    public void Clear()
    {
        // Evicted payload files are keyed by GUID, so other projects' entries never collide —
        // but they DO live in the same session folder.
        if (_histories.Count <= 1)
        {
            // Sole history: one folder-wide wipe is cheaper than per-operation deletes.
            _undoOperations.Clear();
            _redoOperations.Clear();
            _diskCache.ClearAll();
        }
        else
        {
            // Other open tabs still own evicted payloads in the same folder, so delete only this
            // project's files (ClearAll would destroy theirs), then drop the in-memory entries.
            foreach (var operation in _undoOperations)
                DiscardOperation(operation);
            foreach (var operation in _redoOperations)
                DiscardOperation(operation);
            _undoOperations.Clear();
            _redoOperations.Clear();
        }

        _currentOperation = null;
        GC.Collect();
    }


    protected virtual void OnOperationInvoked(OperationInvokeEventArgs e)
    {
        OperationInvoked?.Invoke(this, e);
        Messenger.Default.Send(new OperationInvokedMessage(e.OperationType, e.Operation));
    }

    /// <summary>
    /// Undo/redo stack with a hard cap. Every member takes the same lock and enumeration hands out a
    /// snapshot: pushes are supposed to come from the UI thread, but a debounced operation used to be
    /// pushed straight from a timer's threadpool thread, and the resulting concurrent mutation surfaced
    /// as "Collection was modified after the enumerator was instantiated" out of the enumerating callers
    /// (<see cref="UpdateCacheStates"/>, <see cref="IsAlreadyPushed"/>) — appstat, 3.11.2. The caller was
    /// fixed; this makes the class itself refuse to corrupt, since a 100-item copy costs nothing next to
    /// the edit operation being pushed.
    /// </summary>
    private class LimitedSizeStack<T>(int maxSize) : IEnumerable<T>
    {
        private readonly LinkedList<T> _list = [];

        public Action<T>? OnRemoveItem { get; set; }

        public void Push(T item)
        {
            T? evicted = default;
            var hasEvicted = false;

            lock (_list)
            {
                _list.AddFirst(item);
                if (_list.Count > maxSize)
                {
                    if (_list.Last == null)
                        throw new Exception("No items in stack!");

                    evicted = _list.Last.Value;
                    hasEvicted = true;
                    _list.RemoveLast();
                }
            }

            // Outside the lock: OnRemoveItem deletes the operation's disk cache and disposes it.
            if (hasEvicted)
                OnRemoveItem?.Invoke(evicted!);
        }

        public T Pop()
        {
            lock (_list)
            {
                if (_list.First == null)
                    throw new Exception("No items in stack!");

                var item = _list.First.Value!;
                _list.RemoveFirst();
                return item;
            }
        }

        public int Count
        {
            get
            {
                lock (_list) return _list.Count;
            }
        }

        /// <summary>
        /// Drops every item from <paramref name="index"/> down to the bottom and returns them, so the
        /// caller can release whatever they own. Unlike overflow eviction this is not a size limit, so it
        /// deliberately does not fire <see cref="OnRemoveItem"/> — the caller is already holding the items.
        /// </summary>
        public IReadOnlyList<T> TruncateFrom(int index)
        {
            var removed = new List<T>();

            lock (_list)
            {
                while (_list.Count > index && _list.Last != null)
                {
                    removed.Add(_list.Last.Value);
                    _list.RemoveLast();
                }
            }

            return removed;
        }

        public IEnumerator<T> GetEnumerator()
        {
            T[] snapshot;
            lock (_list) snapshot = _list.ToArray();
            return ((IEnumerable<T>)snapshot).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            lock (_list) _list.Clear();
        }
    }
}