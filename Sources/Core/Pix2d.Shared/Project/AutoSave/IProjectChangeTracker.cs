#nullable enable
namespace Pix2d.Project.AutoSave;

/// <summary>
/// Per-frame dirty tracker. Lives on the UI thread (the messenger that drives it
/// always dispatches on the UI thread); no internal locking is required as long
/// as <see cref="Drain"/> is also called from the UI thread.
/// </summary>
public interface IProjectChangeTracker
{
    /// <summary>True if there is anything to save since the last successful commit.</summary>
    bool HasPendingChanges { get; }

    void MarkLayerFrameDirty(int layerIndex, int frameIndex);

    /// <summary>
    /// Marks the scene tree (layer/frame structure, sizes, properties, etc.) as changed.
    /// Triggers a fresh <c>scene.json</c> on the next snapshot but does not by itself
    /// flag any frames as dirty.
    /// </summary>
    void MarkStructureDirty();

    /// <summary>
    /// Worst-case fallback for operations that do not implement
    /// <see cref="IFrameAffectingOperation"/>. Marks all currently-known frames as dirty
    /// and forces a structural rewrite.
    /// </summary>
    void MarkAllDirty();

    /// <summary>
    /// Atomically returns the pending change set and resets it. Subsequent
    /// modifications go into a fresh set so that — if the commit fails — we can
    /// re-mark them dirty via <see cref="Reapply"/>.
    /// </summary>
    DirtySet Drain();

    /// <summary>Re-applies a change set after a failed commit attempt.</summary>
    void Reapply(DirtySet pending);
}

/// <summary>Snapshot of accumulated dirty state.</summary>
public sealed class DirtySet
{
    public bool StructureChanged { get; init; }
    public IReadOnlySet<(int Layer, int Frame)> DirtyCells { get; init; } = new HashSet<(int, int)>();

    public bool IsEmpty => !StructureChanged && DirtyCells.Count == 0;

    public static DirtySet Empty { get; } = new();
}
