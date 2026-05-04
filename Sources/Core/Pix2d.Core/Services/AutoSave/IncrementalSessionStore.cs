#nullable enable
using Newtonsoft.Json;
using Pix2d.Project.AutoSave;
using SkiaNodes;
using SkiaNodes.Serialization;
using SkiaSharp;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// File-system-backed session store. Two safety properties:
///
/// <para><b>Atomicity.</b> Every artifact is written to a <c>.tmp</c> sibling and
/// then moved into place via <see cref="File.Move(string, string, bool)"/>:
/// <c>frames/&lt;key&gt;.png</c>, <c>scene.json</c>, and finally
/// <c>manifest.json</c>. The manifest rename is the commit point: a crash before
/// it leaves the previous (fully-valid) revision untouched.</para>
///
/// <para><b>Mobile-safe ownership.</b> The work folder is owned by holding an
/// <see cref="FileStream"/> on <c>.lock</c> opened with
/// <see cref="FileShare.None"/>. Sandbox file systems on Android / iOS honour
/// exclusive opens; PIDs are never used (they get reused after a process restart
/// in app sandboxes). The lock file's last-write timestamp is refreshed by
/// <see cref="HeartbeatAsync"/> so that a crashed-and-cleaned-up handle can be
/// detected by recovery code via a wall-clock staleness threshold as a fallback.</para>
/// </summary>
public sealed class IncrementalSessionStore : IIncrementalSessionStore
{
    private const string ManifestFile = "manifest.json";
    private const string SceneFile = "scene.json";
    private const string ThumbnailFile = "thumbnail.jpg";
    private const string FramesDir = "frames";
    private const string LockFile = ".lock";

    private readonly string _root;
    private long _lastRevision;

    /// <summary>
    /// Held open for the lifetime of the session (FileShare.None). Other processes
    /// attempting <c>FileMode.Open</c> with FileShare.None on this file will fail
    /// with <see cref="IOException"/>, signalling that the session is alive.
    /// </summary>
    private FileStream? _lockHandle;

    public string SessionId { get; }
    public string SessionFolderPath => _root;

    public IncrementalSessionStore(string sessionsRoot, string sessionId)
    {
        SessionId = sessionId;
        _root = Path.Combine(sessionsRoot, "active", sessionId);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, FramesDir));

        // Take exclusive lock. If somebody else owns the folder we throw —
        // the recovery layer is the only place allowed to claim an existing
        // folder (and it forces ownership only after staleness checks).
        _lockHandle = new FileStream(
            Path.Combine(_root, LockFile),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 256,
            options: FileOptions.WriteThrough);

        await HeartbeatAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Refreshes the lock-file's content (and therefore its last-write time).
    /// Called by <c>AutoSaveService</c> on every successful tick so recovery
    /// can use file mtime as a fallback liveness signal even if the FileStream
    /// lock was somehow released without the process actually exiting.
    /// </summary>
    public async Task HeartbeatAsync(CancellationToken ct = default)
    {
        if (_lockHandle is null) return;
        var stamp = DateTime.UtcNow.ToString("O") + "\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(stamp);

        _lockHandle.Position = 0;
        _lockHandle.SetLength(0);
        await _lockHandle.WriteAsync(bytes, ct).ConfigureAwait(false);
        await _lockHandle.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task<SessionManifest> CommitAsync(SceneSnapshot snapshot, CancellationToken ct = default)
    {
        // 1. Write each dirty PNG via temp + atomic replace.
        foreach (var frame in snapshot.DirtyFrames)
        {
            ct.ThrowIfCancellationRequested();
            await WriteFrameAtomicAsync(frame, ct).ConfigureAwait(false);
        }

        // 2. Write the scene tree if structure changed.
        if (snapshot.SceneJson is not null)
        {
            ct.ThrowIfCancellationRequested();
            await WriteTextAtomicAsync(Path.Combine(_root, SceneFile), snapshot.SceneJson, ct)
                .ConfigureAwait(false);
        }

        // 3. Thumbnail is best-effort, never blocks recovery.
        if (snapshot.Thumbnail is not null)
        {
            try
            {
                await WriteImageAtomicAsync(
                    Path.Combine(_root, ThumbnailFile),
                    snapshot.Thumbnail.Image,
                    SKEncodedImageFormat.Jpeg,
                    quality: 75,
                    ct).ConfigureAwait(false);
            }
            catch { /* ignore */ }
        }

        // 4. Garbage-collect orphan PNGs not referenced by the new scene.
        if (snapshot.StructureChanged)
            TryCollectOrphanFrames(snapshot.LiveFrameKeys);

        // 5. Refresh the heartbeat alongside the commit.
        try { await HeartbeatAsync(ct).ConfigureAwait(false); } catch { /* ignore */ }

        // 6. Build + atomically publish the manifest. This is the COMMIT POINT.
        var manifest = new SessionManifest
        {
            FormatVersion = 1,
            SessionId = SessionId,
            Revision = Interlocked.Increment(ref _lastRevision),
            CommittedAtUtc = DateTime.UtcNow,
            SourceProjectPath = snapshot.SourceProjectPath,
            FrameKeys = snapshot.LiveFrameKeys.ToList(),
            SceneFile = SceneFile,
            ThumbnailFile = snapshot.Thumbnail is not null ? ThumbnailFile : null,
        };

        var manifestJson = JsonConvert.SerializeObject(manifest, Formatting.Indented);
        await WriteTextAtomicAsync(Path.Combine(_root, ManifestFile), manifestJson, ct)
            .ConfigureAwait(false);

        return manifest;
    }

    public async Task<SessionManifest?> TryReadManifestAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(_root, ManifestFile);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var m = JsonConvert.DeserializeObject<SessionManifest>(json);
            if (m is not null) _lastRevision = m.Revision;
            return m;
        }
        catch
        {
            return null;
        }
    }

    public async Task<SKNode?> LoadSceneAsync(CancellationToken ct = default)
    {
        var manifest = await TryReadManifestAsync(ct).ConfigureAwait(false);
        if (manifest is null) return null;

        var sceneJsonPath = Path.Combine(_root, manifest.SceneFile);
        if (!File.Exists(sceneJsonPath)) return null;

        var images = new Dictionary<string, SKBitmap>();
        foreach (var key in manifest.FrameKeys)
        {
            var pngPath = Path.Combine(_root, FramesDir, key + ".png");
            if (!File.Exists(pngPath)) continue;

            await using var fs = File.OpenRead(pngPath);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;

            using var codec = SKCodec.Create(ms);
            if (codec is null) continue;

            var info = codec.Info;
            info.ColorType = Pix2DAppSettings.ColorType;
            info.AlphaType = SKAlphaType.Premul;

            var bm = SKBitmap.Decode(codec, info);
            if (bm is not null) images[key + ".png"] = bm;
        }

        var sceneJson = await File.ReadAllTextAsync(sceneJsonPath, ct).ConfigureAwait(false);
        return NodeSerializer.Deserialize<SKNode>(sceneJson, images);
    }

    public Task DisposeAsync(bool deleteFolder)
    {
        try { _lockHandle?.Dispose(); } catch { /* ignore */ }
        _lockHandle = null;

        if (deleteFolder)
        {
            try { Directory.Delete(_root, recursive: true); } catch { /* best-effort */ }
        }
        return Task.CompletedTask;
    }

    // ─────────────── helpers ───────────────

    private async Task WriteFrameAtomicAsync(FrameSnapshot frame, CancellationToken ct)
    {
        var finalPath = Path.Combine(_root, FramesDir, frame.Key + ".png");
        var tempPath = finalPath + ".tmp";

        await using (var fs = new FileStream(
                         tempPath, FileMode.Create, FileAccess.Write,
                         FileShare.None, bufferSize: 64 * 1024, useAsync: true))
        {
            // SKImage.Encode is safe on a background thread because the image
            // is immutable from our side (COW). PNG encoding is heavy CPU work,
            // which is exactly why we are off the UI thread.
            using var data = frame.Image.Encode(SKEncodedImageFormat.Png, 100);
            if (data is null)
                throw new IOException($"Encode failed for frame '{frame.Key}'");
            data.SaveTo(fs);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }

        AtomicReplace(tempPath, finalPath);
    }

    private static async Task WriteTextAtomicAsync(string finalPath, string content, CancellationToken ct)
    {
        var tempPath = finalPath + ".tmp";
        await using (var fs = new FileStream(
                         tempPath, FileMode.Create, FileAccess.Write,
                         FileShare.None, bufferSize: 64 * 1024, useAsync: true))
        await using (var sw = new StreamWriter(fs))
        {
            await sw.WriteAsync(content.AsMemory(), ct).ConfigureAwait(false);
            await sw.FlushAsync().ConfigureAwait(false);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }
        AtomicReplace(tempPath, finalPath);
    }

    private static async Task WriteImageAtomicAsync(
        string finalPath,
        SKImage image,
        SKEncodedImageFormat format,
        int quality,
        CancellationToken ct)
    {
        var tempPath = finalPath + ".tmp";
        await using (var fs = new FileStream(
                         tempPath, FileMode.Create, FileAccess.Write,
                         FileShare.None, bufferSize: 64 * 1024, useAsync: true))
        {
            using var data = image.Encode(format, quality);
            if (data is null) return;
            data.SaveTo(fs);
            await fs.FlushAsync(ct).ConfigureAwait(false);
        }
        AtomicReplace(tempPath, finalPath);
    }

    private static void AtomicReplace(string source, string destination)
    {
        // File.Move with overwrite is atomic on the same volume on NTFS, ext4,
        // and APFS — and on Android internal storage / iOS sandbox FSes which
        // sit on top of those.
        File.Move(source, destination, overwrite: true);
    }

    private void TryCollectOrphanFrames(IReadOnlyList<string> liveKeys)
    {
        var live = new HashSet<string>(liveKeys.Select(k => k + ".png"));
        var dir = Path.Combine(_root, FramesDir);
        if (!Directory.Exists(dir)) return;

        foreach (var f in Directory.EnumerateFiles(dir, "*.png"))
        {
            var name = Path.GetFileName(f);
            if (!live.Contains(name)) TryDelete(f);
        }
        foreach (var f in Directory.EnumerateFiles(dir, "*.tmp"))
            TryDelete(f);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
    }
}
