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
    private readonly IFileService _fileService;
    private readonly IMessenger _messenger;
    private readonly IProjectChangeTracker _tracker;
    private readonly ISessionSnapshotProvider _snapshotProvider;
    private readonly AutoSaveRecovery _recovery;

    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    private IIncrementalSessionStore? _store;
    private PeriodicTimer? _timer;
    private Task? _loop;

    public bool IsRunning => _loop is { IsCompleted: false };

    public AutoSaveService(
        AppState appState,
        IFileService fileService,
        IMessenger messenger,
        IProjectChangeTracker tracker,
        ISessionSnapshotProvider snapshotProvider)
    {
        _appState = appState;
        _fileService = fileService;
        _messenger = messenger;
        _tracker = tracker;
        _snapshotProvider = snapshotProvider;

        var sessionsRoot = ResolveSessionsRoot(fileService);
        _recovery = new AutoSaveRecovery(sessionsRoot);
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        if (_store is null)
        {
            var sessionsRoot = ResolveSessionsRoot(_fileService);
            _store = new IncrementalSessionStore(sessionsRoot, Guid.NewGuid().ToString("N"));
            await _store.InitializeAsync().ConfigureAwait(false);
        }

        var period = _appState.Settings.AutoSaveInterval;
        if (period <= TimeSpan.Zero) period = TimeSpan.FromSeconds(30);

        _timer = new PeriodicTimer(period);
        _loop = RunLoopAsync(_timer, _cts.Token);
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

    public async Task<bool> TryRecoverAsync()
    {
        if (OperatingSystem.IsBrowser()) return false;

        var loaded = await _recovery.LoadMostRecentAsync(_cts.Token).ConfigureAwait(false);
        if (loaded is null) return false;

        var (scene, store) = loaded.Value;
        _store = store;

        // Inject the recovered scene back into the editor on the UI thread.
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

    // ─────────────── helpers ───────────────

    private static string ResolveSessionsRoot(IFileService fs)
    {
        // GetLocalFolderAsync returns a platform-native folder; for the sessions
        // *root* we need a synchronous string path because IncrementalSessionStore
        // uses System.IO directly (we can do that safely on desktop / Android,
        // and we no-op on Browser through the IsBrowser() check above).
        // Fall back to ApplicationData when that abstraction is not async-friendly.
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Pix2d", "Sessions");
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
