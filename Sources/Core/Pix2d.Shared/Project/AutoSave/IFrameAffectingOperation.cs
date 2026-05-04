#nullable enable
namespace Pix2d.Project.AutoSave;

/// <summary>
/// Implemented by edit operations that know exactly which (layer, frame) cells they touch.
/// The auto-save dirty tracker uses this to mark a minimal set of frame snapshots as dirty
/// instead of falling back to a full re-snapshot of the whole project.
/// </summary>
public interface IFrameAffectingOperation
{
    IReadOnlyCollection<int> AffectedLayerIndexes { get; }
    IReadOnlyCollection<int> AffectedFrameIndexes { get; }
}
