#nullable enable
namespace Pix2d.Project.AutoSave;

/// <summary>
/// Per-frame dirty tracker. Lives on the UI thread (the messenger that drives it
/// always dispatches on the UI thread); no internal locking is required as long
/// as <see cref="Drain()"/> is also called from the UI thread.
///
/// Dirty cells are bucketed per open project (tab): marks made while a project is
/// active are attributed to that project's bucket, so a tab switch never loses
/// the outgoing project's pending changes — they stay parked until the autosave
/// loop commits them into that project's own session store.
/// </summary>
public interface IProjectChangeTracker
{
    /// <summary>True if any project has anything to save since its last successful commit.</summary>
    bool HasPendingChanges { get; }

    void MarkLayerFrameDirty(int layerIndex, int frameIndex);

    /// <summary>
    /// Marks the scene tree (layer/frame structure, sizes, properties, etc.) as changed.
    /// Triggers a fresh <c>scene.json</c> on the next snapshot but does not by itself
    /// flag any frames as dirty. Applies to the currently active project.
    /// </summary>
    void MarkStructureDirty();

    /// <summary>
    /// Worst-case fallback for operations that do not implement
    /// <see cref="IFrameAffectingOperation"/>. Marks all currently-known frames as dirty
    /// and forces a structural rewrite. Applies to the currently active project.
    /// </summary>
    void MarkAllDirty();

    /// <summary>Same as <see cref="MarkAllDirty()"/> but for an explicit project bucket.</summary>
    void MarkAllDirty(Guid projectId);

    /// <summary>
    /// Atomically returns the pending change set of the ACTIVE project and resets it.
    /// Subsequent modifications go into a fresh set so that — if the commit fails — we
    /// can re-mark them dirty via <see cref="Reapply(DirtySet)"/>.
    /// </summary>
    DirtySet Drain();

    /// <summary>Drains the pending change set of an explicit project bucket.</summary>
    DirtySet Drain(Guid projectId);

    /// <summary>Project ids that currently have a non-empty pending change set.</summary>
    IReadOnlyList<Guid> GetDirtyProjectIds();

    /// <summary>Re-applies a change set of the ACTIVE project after a failed commit attempt.</summary>
    void Reapply(DirtySet pending);

    /// <summary>Re-applies a change set into an explicit project bucket after a failed commit.</summary>
    void Reapply(Guid projectId, DirtySet pending);

    /// <summary>Drops the bucket of a closed project.</summary>
    void Forget(Guid projectId);
}

/// <summary>Snapshot of accumulated dirty state.</summary>
public sealed class DirtySet
{
    public bool StructureChanged { get; init; }
    public IReadOnlySet<(int Layer, int Frame)> DirtyCells { get; init; } = new HashSet<(int, int)>();

    public bool IsEmpty => !StructureChanged && DirtyCells.Count == 0;

    public static DirtySet Empty { get; } = new();
}
