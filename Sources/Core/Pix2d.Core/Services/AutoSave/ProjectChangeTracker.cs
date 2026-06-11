#nullable enable
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Project.AutoSave;
using Pix2d.State;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Maintains a per-(layer, frame) dirty set fed by <see cref="OperationInvokedMessage"/>,
/// bucketed per open project (tab). Marks are attributed to the project that is active
/// when the operation lands, so a tab switch never discards pending changes — each
/// bucket is drained and committed into its own session store by AutoSaveService.
///
/// Threading: <see cref="OperationService"/> publishes the message synchronously on
/// the UI thread, and <see cref="AutoSaveService"/> calls the drain methods from
/// the UI thread too. No locks needed.
/// </summary>
public sealed class ProjectChangeTracker : IProjectChangeTracker, IDisposable
{
    private sealed class Bucket
    {
        public HashSet<(int Layer, int Frame)> DirtyCells = [];
        public bool StructureChanged;

        public bool IsEmpty => !StructureChanged && DirtyCells.Count == 0;
    }

    private readonly IMessenger _messenger;
    private readonly AppState _appState;

    private readonly Dictionary<Guid, Bucket> _buckets = new();

    public ProjectChangeTracker(IMessenger messenger, AppState appState)
    {
        _messenger = messenger;
        _appState = appState;
        _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    private Bucket GetBucket(Guid projectId)
    {
        if (!_buckets.TryGetValue(projectId, out var bucket))
            _buckets[projectId] = bucket = new Bucket();
        return bucket;
    }

    private Bucket CurrentBucket => GetBucket(_appState.CurrentProject.Id);

    public bool HasPendingChanges => _buckets.Values.Any(b => !b.IsEmpty)
                                     || _appState.CurrentProject.HasUnsavedChanges;

    public void MarkLayerFrameDirty(int layerIndex, int frameIndex)
    {
        if (layerIndex < 0 || frameIndex < 0) return;
        CurrentBucket.DirtyCells.Add((layerIndex, frameIndex));
    }

    public void MarkStructureDirty() => CurrentBucket.StructureChanged = true;

    public void MarkAllDirty() => CurrentBucket.StructureChanged = true;

    public void MarkAllDirty(Guid projectId) => GetBucket(projectId).StructureChanged = true;

    public DirtySet Drain() => Drain(_appState.CurrentProject.Id);

    public DirtySet Drain(Guid projectId)
    {
        if (!_buckets.TryGetValue(projectId, out var bucket) || bucket.IsEmpty)
            return DirtySet.Empty;

        var snapshot = new DirtySet
        {
            StructureChanged = bucket.StructureChanged,
            DirtyCells = bucket.DirtyCells,
        };
        bucket.DirtyCells = [];
        bucket.StructureChanged = false;
        return snapshot;
    }

    public IReadOnlyList<Guid> GetDirtyProjectIds() =>
        _buckets.Where(kv => !kv.Value.IsEmpty).Select(kv => kv.Key).ToList();

    public void Reapply(DirtySet pending) => Reapply(_appState.CurrentProject.Id, pending);

    public void Reapply(Guid projectId, DirtySet pending)
    {
        var bucket = GetBucket(projectId);
        if (pending.StructureChanged) bucket.StructureChanged = true;
        foreach (var c in pending.DirtyCells) bucket.DirtyCells.Add(c);
    }

    public void Forget(Guid projectId) => _buckets.Remove(projectId);

    private void OnOperationInvoked(OperationInvokedMessage msg)
    {
        // Heuristic: any non-Info operation modifies project state.
        if (msg.OperationType == OperationEventType.Info ||
            msg.OperationType == OperationEventType.Command)
            return;

        // Selection-flow ops describe transient marquee/transform state. Only the commit step persists
        // pixels, and it additionally implements ISpriteEditorOperation, so it still flows through the
        // framed-cell branch below. Without this guard, simply drawing a marquee would call MarkAllDirty
        // and force autosave to re-snapshot the whole project.
        if (msg.Operation is ISelectionFlowOperation && msg.Operation is not ISpriteEditorOperation)
            return;

        if (msg.Operation is ISpriteEditorOperation framed &&
            framed.AffectedLayerIndexes.Count > 0 &&
            framed.AffectedFrameIndexes.Count > 0)
        {
            foreach (var li in framed.AffectedLayerIndexes)
                foreach (var fi in framed.AffectedFrameIndexes)
                    MarkLayerFrameDirty(li, fi);
            return;
        }

        // Conservative fallback: structural change. The snapshot provider will
        // re-snapshot every frame on the next tick, which is correct but heavier.
        MarkAllDirty();
    }

    public void Dispose()
    {
        _messenger.Unregister<OperationInvokedMessage>(this);
    }
}
