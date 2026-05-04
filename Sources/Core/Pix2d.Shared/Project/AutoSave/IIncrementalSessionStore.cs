#nullable enable
using SkiaNodes;

namespace Pix2d.Project.AutoSave;

/// <summary>
/// Persists snapshots into a per-session work folder, atomically.
/// All methods are intended to be called from a single background thread.
/// </summary>
public interface IIncrementalSessionStore
{
    string SessionId { get; }

    string SessionFolderPath { get; }

    /// <summary>Initializes (creates / claims) the work folder. Writes <c>.lock</c> with the current PID.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies a snapshot:
    /// 1. Writes each dirty PNG via temp + atomic rename.
    /// 2. Writes <c>scene.json</c> if the snapshot contains it.
    /// 3. Commits a new <see cref="SessionManifest"/> via temp + atomic rename
    ///    — only after every other file is on disk.
    /// 4. GCs orphan <c>frames/*.png</c> files (those not in <see cref="SceneSnapshot.LiveFrameKeys"/>).
    /// </summary>
    Task<SessionManifest> CommitAsync(SceneSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Reads the latest committed manifest, or <c>null</c> if nothing was ever committed.</summary>
    Task<SessionManifest?> TryReadManifestAsync(CancellationToken ct = default);

    /// <summary>Reconstructs an SKNode tree from the current committed state of the work folder.</summary>
    Task<SKNode?> LoadSceneAsync(CancellationToken ct = default);

    /// <summary>Releases the lock and (optionally) wipes the folder.</summary>
    Task DisposeAsync(bool deleteFolder);
}
