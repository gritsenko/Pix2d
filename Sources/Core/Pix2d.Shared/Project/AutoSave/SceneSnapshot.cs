#nullable enable
namespace Pix2d.Project.AutoSave;

/// <summary>
/// Result of a single UI-thread snapshot pass. Holds:
/// <list type="bullet">
///   <item>the serialized scene tree (already a managed string — fully detached from Skia);</item>
///   <item>independent <see cref="FrameSnapshot"/> copies of every dirty bitmap;</item>
///   <item>the optional thumbnail bitmap (also a copy).</item>
/// </list>
/// Once produced, the snapshot is fully detached from the live scene graph and can
/// be processed entirely on a background thread — no further UI-thread interaction
/// is required to commit it to disk.
///
/// <para>
/// <see cref="StructureChanged"/> indicates whether the scene tree itself
/// (layer/frame add/remove/reorder, sizes, etc.) changed since the previous commit.
/// When <c>false</c> the store can skip rewriting <c>scene.json</c>.
/// </para>
/// </summary>
public sealed class SceneSnapshot : IDisposable
{
    public SceneSnapshot(
        string? sceneJson,
        bool structureChanged,
        IReadOnlyList<FrameSnapshot> dirtyFrames,
        IReadOnlyList<string> liveFrameKeys,
        FrameSnapshot? thumbnail,
        string? sourceProjectPath)
    {
        SceneJson = sceneJson;
        StructureChanged = structureChanged;
        DirtyFrames = dirtyFrames;
        LiveFrameKeys = liveFrameKeys;
        Thumbnail = thumbnail;
        SourceProjectPath = sourceProjectPath;
    }

    /// <summary>Serialized SKNode tree (without bitmap pixels). <c>null</c> when nothing structural changed.</summary>
    public string? SceneJson { get; }

    public bool StructureChanged { get; }

    /// <summary>Frame copies that need to be flushed to disk.</summary>
    public IReadOnlyList<FrameSnapshot> DirtyFrames { get; }

    /// <summary>
    /// All frame keys that the current scene references. Used by the store
    /// to garbage-collect <c>frames/*.png</c> files that are no longer referenced
    /// (e.g. after the user deleted a layer/frame).
    /// </summary>
    public IReadOnlyList<string> LiveFrameKeys { get; }

    public FrameSnapshot? Thumbnail { get; }

    public string? SourceProjectPath { get; }

    public void Dispose()
    {
        foreach (var f in DirtyFrames) f.Dispose();
        Thumbnail?.Dispose();
    }
}
