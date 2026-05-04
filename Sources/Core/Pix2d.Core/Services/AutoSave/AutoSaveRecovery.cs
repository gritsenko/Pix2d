#nullable enable
using Pix2d.Project.AutoSave;
using SkiaNodes;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Locates orphan session folders (left over from a previous crash or sandbox kill)
/// and rebuilds the <see cref="SKNode"/> scene from the most recent committed manifest.
///
/// <para><b>Liveness check.</b> We deliberately do NOT use process IDs: on Android
/// and iOS sandboxes the OS reuses PIDs, and a freshly-spawned unrelated app can
/// share the PID of a previous Pix2d process. Instead we use two layered signals:</para>
///
/// <list type="number">
///   <item>The lock file is held by the live process via
///   <see cref="FileShare.None"/>. <see cref="TryProbeOrphan"/> tries to re-open it
///   exclusively; on success we know nobody owns the file system handle anymore.</item>
///   <item>If the exclusive open fails (locked by something), we fall back to the
///   wall-clock age of the file: a heartbeat older than
///   <see cref="StaleHeartbeatThreshold"/> means the holder is long gone, and we
///   take ownership anyway.</item>
/// </list>
///
/// <para>
/// Both signals are sandbox-friendly: exclusive opens are honoured by all the
/// platform file systems we ship on, and mtimes survive process death.
/// </para>
/// </summary>
public sealed class AutoSaveRecovery
{
    /// <summary>If a lock file's heartbeat is older than this, we treat it as stale.</summary>
    public static readonly TimeSpan StaleHeartbeatThreshold = TimeSpan.FromMinutes(1);

    private readonly string _sessionsRoot;

    public AutoSaveRecovery(string sessionsRoot)
    {
        _sessionsRoot = sessionsRoot;
    }

    public sealed record RecoveredSession(string SessionId, string FolderPath, SessionManifest Manifest);

    public IEnumerable<RecoveredSession> EnumerateOrphanSessions()
    {
        var activeRoot = Path.Combine(_sessionsRoot, "active");
        if (!Directory.Exists(activeRoot)) yield break;

        foreach (var folder in Directory.EnumerateDirectories(activeRoot))
        {
            var sessionId = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(sessionId)) continue;

            if (!TryProbeOrphan(folder)) continue;

            var manifestPath = Path.Combine(folder, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            SessionManifest? manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<SessionManifest>(json);
            }
            catch
            {
                continue;
            }

            if (manifest is null) continue;
            yield return new RecoveredSession(sessionId, folder, manifest);
        }
    }

    /// <summary>
    /// Tries to load the most recent orphan session and hand it back to the
    /// caller along with a freshly-claimed <see cref="IIncrementalSessionStore"/>.
    /// The store has already taken its own exclusive lock — subsequent crashes
    /// will find this session under our PID-less ownership scheme.
    /// </summary>
    public async Task<(SKNode Scene, IIncrementalSessionStore Store)?> LoadMostRecentAsync(
        CancellationToken ct = default)
    {
        var best = EnumerateOrphanSessions()
            .OrderByDescending(s => s.Manifest.CommittedAtUtc)
            .ThenByDescending(s => s.Manifest.Revision)
            .FirstOrDefault();

        if (best is null) return null;

        var store = new IncrementalSessionStore(_sessionsRoot, best.SessionId);
        try
        {
            await store.InitializeAsync(ct).ConfigureAwait(false); // exclusive lock
        }
        catch (IOException)
        {
            // Race: another process claimed it between our probe and our open.
            return null;
        }

        var scene = await store.LoadSceneAsync(ct).ConfigureAwait(false);
        if (scene is null)
        {
            await store.DisposeAsync(deleteFolder: false).ConfigureAwait(false);
            return null;
        }

        return (scene, store);
    }

    /// <summary>
    /// Returns true if the session folder is unowned and safe to claim.
    /// Tries the exclusive-open probe first; falls back to the staleness check
    /// only if the probe fails.
    /// </summary>
    private static bool TryProbeOrphan(string sessionFolder)
    {
        var lockPath = Path.Combine(sessionFolder, ".lock");
        if (!File.Exists(lockPath))
        {
            // No lock file at all — either freshly initialised or pre-lock-format
            // session. Treat as orphan; the caller will re-create the lock anyway.
            return true;
        }

        // 1. Exclusive-open probe. If we get the handle, the previous holder
        //    has disappeared (process exit, OOM kill, tombstoning) and the
        //    OS has released its FileShare.None reservation.
        try
        {
            using var probe = new FileStream(
                lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return true;
        }
        catch (IOException)
        {
            // Locked. Fall through to staleness check.
        }
        catch (UnauthorizedAccessException)
        {
            return false; // permission issue, leave it alone
        }

        // 2. Time-based fallback. Some platforms (or some FUSE / sync clients)
        //    do not always release FileShare.None promptly; if the heartbeat
        //    is older than our threshold we conclude the owner is dead anyway.
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(lockPath);
            return DateTime.UtcNow - lastWrite > StaleHeartbeatThreshold;
        }
        catch
        {
            return false;
        }
    }
}
