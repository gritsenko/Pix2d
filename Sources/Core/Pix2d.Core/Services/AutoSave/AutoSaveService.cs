#nullable enable
using Avalonia.Threading;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Messages;
using Pix2d.Project.AutoSave;
using Pix2d.State;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Orchestrates the new auto-save pipeline:
/// <code>
///   PeriodicTimer → tracker.Drain                       (UI thread)
///                 → snapshotProvider.TakeAsync          (UI thread, SKImage.FromBitmap, COW)
///                 → store.CommitAsync                   (background, atomic rename)
/// </code>
/// Replaces the legacy <see cref="ISessionService"/>. The legacy interface is
/// implemented as a thin adapter so existing callers (commands, shutdown hooks)
/// keep working unmodified during the migration.
/// </summary>
public sealed class AutoSaveService : IAutoSaveService, ISessionService, IAsyncDisposable
{
    private readonly AppState _appState;
    private readonly IPlatformStuffService _platformStuff;
    private readonly IMessenger _messenger;
    private readonly IProjectChangeTracker _tracker;
    private readonly ISessionSnapshotProvider _snapshotProvider;
    private readonly AutoSaveRecovery _recovery;
    private readonly string _sessionsRoot;

    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private IIncrementalSessionStore? _store;
    private PeriodicTimer? _timer;
    private Task? _loop;

    public bool IsRunning => _loop is { IsCompleted: false };

    public AutoSaveService(
        AppState appState,
        IPlatformStuffService platformStuff,
        IMessenger messenger,
        IProjectChangeTracker tracker,
        ISessionSnapshotProvider snapshotProvider)
    {
        _appState = appState;
        _platformStuff = platformStuff;
        _messenger = messenger;
        _tracker = tracker;
        _snapshotProvider = snapshotProvider;

        _sessionsRoot = ResolveSessionsRoot(platformStuff);
        _recovery = new AutoSaveRecovery(_sessionsRoot);
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        if (_store is null)
        {
            _store = new IncrementalSessionStore(_sessionsRoot, Guid.NewGuid().ToString("N"));
            await _store.InitializeAsync().ConfigureAwait(false);
            Logger.Log($"AutoSave: initialized session store under {_sessionsRoot}");
        }

        var period = _appState.Settings.AutoSaveInterval;
        if (period <= TimeSpan.Zero) period = TimeSpan.FromSeconds(30);

        _timer = new PeriodicTimer(period);
        _loop = RunLoopAsync(_timer, _cts.Token);
        Logger.Log($"AutoSave: started periodic loop ({period.TotalSeconds:0.#}s)");
    }

    public async Task StopAsync()
    {
        _timer?.Dispose();
        _timer = null;

        try { await _cts.CancelAsync().ConfigureAwait(false); } catch { /* ignore */ }
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch { /* normal cancel */ }
        }
        _loop = null;

        // One last best-effort flush.
        await TickOnceAsync(forceFullSnapshot: false).ConfigureAwait(false);
    }

    public Task ForceSaveAsync(TimeSpan timeout)
        => TickOnceAsync(forceFullSnapshot: true, timeout: timeout);

    public void ForceSaveSync(TimeSpan timeout)
    {
        // Android lifecycle callbacks (OnPause / OnStop / IActivatableLifetime.Deactivated)
        // run on the Avalonia UI thread. We CANNOT bounce the save through a
        // worker task that needs to come back to the UI thread via
        // Dispatcher.UIThread.InvokeAsync — the UI thread would already be
        // blocked on .Wait(...), causing a deadlock until the bounded timeout
        // expires and the save silently fails. That's the "session lost on
        // double-back" path the previous fix didn't catch: the SIGKILL was
        // gone, but the save still wasn't actually completing.
        //
        // Instead we run drain + snapshot SYNCHRONOUSLY on the calling thread
        // (we are on the UI thread, so no marshalling is needed) and only
        // block on the commit, which is pure file I/O without dispatcher
        // round-trips.
        Dispatcher.UIThread.VerifyAccess();
        if (_store is null)
        {
            Logger.Log("AutoSave: force-save skipped because the session store is not initialized");
            return;
        }

        // If a periodic / async save is in flight we cannot wait on it from
        // here — that save will need to come back to this same UI thread via
        // InvokeAsync to drain. Skip; the in-flight save is doing its job and
        // the next callback will pick up anything new.
        if (!_commitLock.Wait(0)) return;

        DirtySet drained = DirtySet.Empty;
        SceneSnapshot? snapshot = null;
        try
        {
            // 1. Drain (UI thread).
            _tracker.MarkAllDirty();
            if (!_tracker.HasPendingChanges) return;
            drained = _tracker.Drain();
            if (drained.IsEmpty) return;

            // 2. Snapshot (UI thread, COW — cheap).
            var scene = _appState.CurrentProject.SceneNode;
            var srcPath = _appState.CurrentProject.File?.Path;
            snapshot = _snapshotProvider.TakeSync(scene, drained, srcPath);
            if (snapshot is null) return;

            // 3. Commit on a background thread; block UI thread on the commit
            //    only. No dispatcher calls happen inside CommitAsync, so the
            //    bounded wait here is safe.
            var localSnap = snapshot;
            var commitTask = Task.Run(() => _store.CommitAsync(localSnap, CancellationToken.None));
            try
            {
                if (!commitTask.Wait(timeout))
                {
                    // Commit didn't finish in time. Push dirty cells back so
                    // they retry on the next tick. We are still on the UI
                    // thread so Reapply is safe to call directly.
                    if (!drained.IsEmpty) _tracker.Reapply(drained);
                }
                else
                {
                    Logger.Log("AutoSave: synchronous force-save committed");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                if (!drained.IsEmpty) _tracker.Reapply(drained);
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            if (!drained.IsEmpty)
            {
                try { _tracker.Reapply(drained); } catch { /* shutdown */ }
            }
        }
        finally
        {
            snapshot?.Dispose();
            _commitLock.Release();
        }
    }

    public async Task<bool> TryRecoverAsync()
    {
        if (OperatingSystem.IsBrowser()) return false;

        var loaded = await _recovery.LoadMostRecentAsync(_cts.Token).ConfigureAwait(false);
        if (loaded is null)
        {
            Logger.Log($"AutoSave: no recoverable session found under {_sessionsRoot}");
            return false;
        }

        var (scene, store) = loaded.Value;
        _store = store;
        Logger.Log($"AutoSave: recovered previous session from {_sessionsRoot}");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _appState.CurrentProject.HasUnsavedChanges = true;
            _messenger.Send(new ProjectLoadedMessage(scene));
        });
        return true;
    }

    // ─────────────── ISessionService adapter (legacy) ───────────────

    Task ISessionService.TrySaveSessionAsync(bool criticalSave)
        => TickOnceAsync(forceFullSnapshot: criticalSave);

    Task ISessionService.TryLoadSessionAsync()
        => StartAsyncWithRecovery();

    private async Task StartAsyncWithRecovery()
    {
        try { await TryRecoverAsync().ConfigureAwait(false); }
        catch (Exception ex) { Logger.LogException(ex); }
        finally { await StartAsync().ConfigureAwait(false); }
    }

    // ─────────────── core loop ───────────────

    private async Task RunLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await TickOnceAsync(forceFullSnapshot: false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (ObjectDisposedException) { /* timer disposed */ }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
    }

    private async Task TickOnceAsync(bool forceFullSnapshot, TimeSpan? timeout = null)
    {
        if (_store is null) return;

        // Non-blocking try-acquire for periodic ticks; bounded wait for forced.
        var acquired = await _commitLock.WaitAsync(
            timeout ?? (forceFullSnapshot ? TimeSpan.FromSeconds(5) : TimeSpan.Zero))
            .ConfigureAwait(false);
        if (!acquired) return;

        SceneSnapshot? snapshot = null;
        DirtySet drained = DirtySet.Empty;
        try
        {
            // 1. Drain dirty set on UI thread (tracker is UI-thread-only).
            drained = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (forceFullSnapshot) _tracker.MarkAllDirty();
                if (!_tracker.HasPendingChanges) return DirtySet.Empty;
                return _tracker.Drain();
            });

            if (drained.IsEmpty) return;

            // 2. Take snapshot on UI thread.
            var scene = _appState.CurrentProject.SceneNode;
            var srcPath = _appState.CurrentProject.File?.Path;
            snapshot = await _snapshotProvider.TakeAsync(scene, drained, srcPath).ConfigureAwait(false);
            if (snapshot is null) return;

            // 3. Commit on background thread (we're already off the UI thread here).
            await Task.Run(() => _store.CommitAsync(snapshot, _cts.Token), _cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            // If commit failed, push dirty cells back so we retry next tick.
            // Tracker is UI-thread-only — bounce through the dispatcher.
            if (!drained.IsEmpty)
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(() => _tracker.Reapply(drained));
                }
                catch { /* shutdown */ }
            }
        }
        finally
        {
            snapshot?.Dispose();
            _commitLock.Release();
        }
    }

    private static string ResolveSessionsRoot(IPlatformStuffService platformStuff)
    {
        return Path.Combine(platformStuff.GetAppFolderPath(), "Sessions");
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync().ConfigureAwait(false); } catch { /* ignore */ }

        // Release the FileShare.None lock on the work folder so the next process
        // can claim it via the normal recovery path. We do NOT delete the folder
        // here — keeping the last manifest around lets the next launch offer
        // "restore previous session" if the user actually wants it.
        if (_store is not null)
        {
            try { await _store.DisposeAsync(deleteFolder: false).ConfigureAwait(false); }
            catch { /* ignore */ }
            _store = null;
        }

        _commitLock.Dispose();
        _cts.Dispose();
    }
}
