#nullable enable
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Project.AutoSave;
using Pix2d.State;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Maintains a per-(layer, frame) dirty set fed by <see cref="OperationInvokedMessage"/>.
///
/// Threading: <see cref="OperationService"/> publishes the message synchronously on
/// the UI thread, and <see cref="AutoSaveService"/> calls <see cref="Drain"/> from
/// the UI thread too. No locks needed.
/// </summary>
public sealed class ProjectChangeTracker : IProjectChangeTracker, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly AppState _appState;

    private HashSet<(int Layer, int Frame)> _dirtyCells = [];
    private bool _structureChanged;

    public ProjectChangeTracker(IMessenger messenger, AppState appState)
    {
        _messenger = messenger;
        _appState = appState;
        _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
    }

    public bool HasPendingChanges => _structureChanged || _dirtyCells.Count > 0
                                     || _appState.CurrentProject.HasUnsavedChanges;

    public void MarkLayerFrameDirty(int layerIndex, int frameIndex)
    {
        if (layerIndex < 0 || frameIndex < 0) return;
        _dirtyCells.Add((layerIndex, frameIndex));
    }

    public void MarkStructureDirty() => _structureChanged = true;

    public void MarkAllDirty() => _structureChanged = true;

    public DirtySet Drain()
    {
        if (!_structureChanged && _dirtyCells.Count == 0)
            return DirtySet.Empty;

        var snapshot = new DirtySet
        {
            StructureChanged = _structureChanged,
            DirtyCells = _dirtyCells,
        };
        _dirtyCells = [];
        _structureChanged = false;
        return snapshot;
    }

    public void Reapply(DirtySet pending)
    {
        if (pending.StructureChanged) _structureChanged = true;
        foreach (var c in pending.DirtyCells) _dirtyCells.Add(c);
    }

    private void OnOperationInvoked(OperationInvokedMessage msg)
    {
        // Heuristic: any non-Info operation modifies project state.
        if (msg.OperationType == OperationEventType.Info ||
            msg.OperationType == OperationEventType.Command)
            return;

        if (msg.Operation is IFrameAffectingOperation framed &&
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
