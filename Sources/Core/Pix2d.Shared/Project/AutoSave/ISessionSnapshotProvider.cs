#nullable enable
using SkiaNodes;

namespace Pix2d.Project.AutoSave;

/// <summary>
/// Builds a <see cref="SceneSnapshot"/> from the live scene graph. The single rule:
/// <see cref="TakeAsync"/> guarantees that every Skia object touched here is read
/// or copied on the UI thread, and the returned <see cref="SceneSnapshot"/> contains
/// only managed strings and independent <c>SKBitmap.Copy()</c> instances —
/// safe to hand off to a background save thread.
/// </summary>
public interface ISessionSnapshotProvider
{
    Task<SceneSnapshot?> TakeAsync(SKNode? scene, DirtySet dirty, string? sourceProjectPath);
}
