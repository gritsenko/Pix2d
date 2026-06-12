#nullable enable
using System.Collections.Concurrent;
using Avalonia.Threading;
using Newtonsoft.Json;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Common.FileSystem;
using Pix2d.Messages;
using Pix2d.Project.AutoSave;
using Pix2d.State;
using SkiaNodes;

namespace Pix2d.Services.AutoSave;

/// <summary>
/// Orchestrates the incremental auto-save pipeline:
/// <code>
///   PeriodicTimer → tracker.Drain(projectId)             (UI thread)
///                 → snapshotProvider.TakeAsync           (UI thread, SKImage.FromBitmap, COW)
///                 → store.CommitAsync                    (background, atomic rename)
/// </code>
/// Replaces the legacy <see cref="ISessionService"/>. The legacy interface is
/// implemented as a thin adapter so existing callers (commands, shutdown hooks)
/// keep working unmodified during the migration.
///
/// <para><b>Multi-project (tabs):</b> every open project owns its own session store
/// (work folder), keyed by <c>ProjectState.Id</c>. The dirty tracker buckets changes
/// per project, so edits parked in a background tab are committed into that tab's
/// store on the next tick. <c>workspace.json</c> at the sessions root records the
/// ordered tab list + active index and is rewritten after every commit / tab change;
/// at startup <see cref="TryRecoverAsync"/> restores ALL listed tabs (falling back to
/// the legacy single most-recent-session recovery when no workspace file exists).
/// Closing a tab deletes its session folder via <see cref="DiscardProjectSessionAsync"/>.</para>
/// </summary>
public sealed class AutoSaveService : IAutoSaveService, ISessionService, IAsyncDisposable
{
    private const string WorkspaceFile = "workspace.json";

    private readonly AppState _appState;
    private readonly IPlatformStuffService _platformStuff;
    private readonly IMessenger _messenger;
    private readonly IProjectChangeTracker _tracker;
    private readonly ISessionSnapshotProvider _snapshotProvider;
    private readonly IProjectActivationService _projectActivation;
    private readonly AutoSaveRecovery _recovery;
    private readonly string _sessionsRoot;

    private readonly SemaphoreSlim _commitLock = new(1, 1);
    private readonly SemaphoreSlim _manifestLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();

    /// <summary>Session store per open project, keyed by <see cref="ProjectState.Id"/>.</summary>
    private readonly ConcurrentDictionary<Guid, IIncrementalSessionStore> _stores = new();

    private PeriodicTimer? _timer;
    private Task? _loop;

    public bool IsRunning => _loop is { IsCompleted: false };

    public AutoSaveService(
        AppState appState,
        IPlatformStuffService platformStuff,
        IMessenger messenger,
        IProjectChangeTracker tracker,
        ISessionSnapshotProvider snapshotProvider,
        IProjectActivationService projectActivation)
    {
        _appState = appState;
        _platformStuff = platformStuff;
        _messenger = messenger;
        _tracker = tracker;
        _snapshotProvider = snapshotProvider;
        _projectActivation = projectActivation;

        _sessionsRoot = ResolveSessionsRoot(platformStuff);
        _recovery = new AutoSaveRecovery(_sessionsRoot);

        // Tab switches / opens / closes change what workspace.json must describe. Handlers run
        // on the UI thread; the manifest data is captured there and written on a worker.
        _messenger.Register<ProjectActivatedMessage>(this, _ => RequestWorkspaceManifestUpdate());
        _messenger.Register<ProjectsListChangedMessage>(this, _ =>
        {
            RequestWorkspaceManifestUpdate();
            // A freshly opened/created tab has no session store yet, so it is absent from
            // workspace.json (and has no data on disk) until the first periodic tick — a crash in
            // that window would lose it. Commit storeless tabs right away.
            RequestEagerPersistOfNewTabs();
        });

        // A save clears the active tab's dirty flag; persist that to workspace.json immediately so a
        // crash before the next commit tick doesn't resurrect the just-saved tab as dirty.
        _messenger.Register<ProjectSavedMessage>(this, _ => RequestWorkspaceManifestUpdate());
    }

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        // Stores are created lazily, one per project, on the first commit for that project
        // (see CollectPendingWork: a project without a store is marked all-dirty so it gets
        // fully committed within one tick of appearing).
        var period = _appState.Settings.AutoSaveInterval;
        if (period <= TimeSpan.Zero) period = TimeSpan.FromSeconds(30);

        _timer = new PeriodicTimer(period);
        _loop = RunLoopAsync(_timer, _cts.Token);
        Logger.Log($"AutoSave: started periodic loop ({period.TotalSeconds:0.#}s)");
        return Task.CompletedTask;
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
        // Lifecycle callbacks (window Closing, Android OnPause) run on the Avalonia UI
        // thread. We CANNOT bounce the save through a worker task that needs to come back
        // to the UI thread via Dispatcher.UIThread.InvokeAsync — the UI thread would be
        // blocked on .Wait(...), deadlocking until the timeout. Instead drain + snapshot
        // run SYNCHRONOUSLY here (we are on the UI thread) and we only block on the
        // commit, which is pure file I/O without dispatcher round-trips.
        Dispatcher.UIThread.VerifyAccess();

        // If a periodic / async save is in flight we cannot wait on it from here — that
        // save needs this same UI thread to drain. Skip; it is doing its job already.
        if (!_commitLock.Wait(0)) return;

        var commits = new List<(IIncrementalSessionStore Store, SceneSnapshot Snapshot)>();
        var reapplyOnFailure = new List<(Guid ProjectId, DirtySet Dirty)>();
        try
        {
            foreach (var project in GetProjectsToSave())
            {
                if (project.SceneNode is null) continue;

                _tracker.MarkAllDirty(project.Id);
                var dirty = _tracker.Drain(project.Id);
                if (dirty.IsEmpty) continue;

                SceneSnapshot? snapshot = null;
                try
                {
                    snapshot = _snapshotProvider.TakeSync(project.SceneNode, dirty, project.File?.Path);
                    if (snapshot is null)
                    {
                        _tracker.Reapply(project.Id, dirty);
                        continue;
                    }

                    commits.Add((GetOrCreateStoreSync(project), snapshot));
                    reapplyOnFailure.Add((project.Id, dirty));
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    snapshot?.Dispose();
                    _tracker.Reapply(project.Id, dirty);
                }
            }

            if (commits.Count == 0) return;

            var manifest = BuildWorkspaceManifest();

            // The task OWNS the snapshots: they are disposed there, never here — if the
            // bounded wait below times out the commit keeps running in the background and
            // must not see disposed images.
            var commitTask = Task.Run(async () =>
            {
                foreach (var (store, snapshot) in commits)
                {
                    try { await store.CommitAsync(snapshot, CancellationToken.None).ConfigureAwait(false); }
                    finally { snapshot.Dispose(); }
                }

                await WriteWorkspaceManifestAsync(manifest).ConfigureAwait(false);
            });

            try
            {
                if (!commitTask.Wait(timeout))
                {
                    // Push dirty cells back so they retry on the next tick. We are still on
                    // the UI thread so Reapply is safe to call directly.
                    foreach (var (projectId, dirty) in reapplyOnFailure)
                        _tracker.Reapply(projectId, dirty);
                }
                else
                {
                    Logger.Log($"AutoSave: synchronous force-save committed {commits.Count} project(s)");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                foreach (var (projectId, dirty) in reapplyOnFailure)
                    _tracker.Reapply(projectId, dirty);
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _commitLock.Release();
        }
    }

    // ─────────────── recovery ───────────────

    public async Task<bool> TryRecoverAsync()
    {
        if (OperatingSystem.IsBrowser()) return false;

        try
        {
            if (await TryRecoverWorkspaceAsync(_cts.Token).ConfigureAwait(false))
                return true;
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        // Legacy / fallback path: the single most recent orphan session becomes the
        // current (placeholder) project.
        var loaded = await _recovery.LoadMostRecentAsync(_cts.Token).ConfigureAwait(false);
        if (loaded is null)
        {
            Logger.Log($"AutoSave: no recoverable session found under {_sessionsRoot}");
            return false;
        }

        var (scene, store) = loaded.Value;
        Logger.Log($"AutoSave: recovered previous session from {_sessionsRoot}");

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // The placeholder CurrentProject receives the scene via the regular fresh-load
            // pipeline, so the claimed store must be keyed to it.
            _stores[_appState.CurrentProject.Id] = store;
            _appState.CurrentProject.HasUnsavedChanges = true;
            _messenger.Send(new ProjectLoadedMessage(scene));
        });
        return true;
    }

    /// <summary>
    /// Restores every tab recorded in <c>workspace.json</c>: claims each session folder,
    /// rebuilds its scene, registers all of them as LoadedProjects and activates the one
    /// that was active in the previous run. Returns false when there is no usable
    /// workspace manifest (first run, corrupt file, or all folders are locked/gone).
    /// </summary>
    private async Task<bool> TryRecoverWorkspaceAsync(CancellationToken ct)
    {
        if (!_platformStuff.SupportsMultipleProjects) return false;

        var manifest = TryReadWorkspaceManifest();
        if (manifest is null || manifest.Tabs.Count == 0) return false;

        var recovered = new List<(ProjectState Project, SKNode Scene, IIncrementalSessionStore Store)>();
        foreach (var tab in manifest.Tabs)
        {
            if (string.IsNullOrWhiteSpace(tab.SessionId)) continue;

            IncrementalSessionStore? store = null;
            try
            {
                var folder = Path.Combine(_sessionsRoot, "active", tab.SessionId);
                if (!Directory.Exists(folder)) continue;

                store = new IncrementalSessionStore(_sessionsRoot, tab.SessionId);
                await store.InitializeAsync(ct).ConfigureAwait(false); // exclusive lock

                var scene = await store.LoadSceneAsync(ct).ConfigureAwait(false);
                if (scene is null)
                {
                    await store.DisposeAsync(deleteFolder: false).ConfigureAwait(false);
                    continue;
                }

                var project = new ProjectState
                {
                    SceneNode = scene,
                    // Restore the dirty state the tab had when the workspace was last persisted:
                    // a tab that was saved (clean) on shutdown comes back clean; one that was ahead
                    // of its backing file comes back dirty so closing it still prompts to save.
                    // Old manifests (pre-"dirty" field) default this to true, preserving the legacy
                    // "everything recovered is dirty" behaviour.
                    HasUnsavedChanges = tab.HasUnsavedChanges,
                };

                if (!string.IsNullOrWhiteSpace(tab.SourceProjectPath) && File.Exists(tab.SourceProjectPath))
                    project.File = new NetFileSource(tab.SourceProjectPath);

                recovered.Add((project, scene, store));
            }
            catch (IOException)
            {
                // Folder is locked — owned by another live Pix2d instance. Leave it alone.
                if (store is not null)
                {
                    try { await store.DisposeAsync(deleteFolder: false).ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
                if (store is not null)
                {
                    try { await store.DisposeAsync(deleteFolder: false).ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
        }

        if (recovered.Count == 0) return false;

        var activeIndex = Math.Clamp(manifest.ActiveIndex, 0, recovered.Count - 1);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var (project, _, store) in recovered)
            {
                _appState.LoadedProjects.Add(project);
                _stores[project.Id] = store;
            }

            var (activeProject, activeScene, _) = recovered[activeIndex];

            // Re-keys the undo history and makes the tab current. The startup placeholder
            // never had a scene, so it was never listed and is simply dropped.
            _projectActivation.BeginNewProjectActivation(activeProject);

            // Regular fresh-load pipeline for the visible tab: scene set, sprite
            // activation, ShowAll, default tool, tab-list reconciliation.
            _messenger.Send(new ProjectLoadedMessage(activeScene));
            _messenger.Send(ProjectsListChangedMessage.Default);
            _messenger.Send(new ProjectActivatedMessage(activeProject));
        });

        Logger.Log($"AutoSave: recovered workspace with {recovered.Count} tab(s) from {_sessionsRoot}");
        return true;
    }

    public async Task DiscardProjectSessionAsync(Guid projectId)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => _tracker.Forget(projectId));
        }
        catch { /* shutdown */ }

        if (!_stores.TryRemove(projectId, out var store))
            return;

        // Serialize against an in-flight tick so we never delete the folder under a commit.
        await _commitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await store.DisposeAsync(deleteFolder: true).ConfigureAwait(false);
            Logger.Log("AutoSave: discarded session of a closed tab");
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _commitLock.Release();
        }
        // The tab-list change already triggers a workspace.json rewrite via ProjectsListChangedMessage.
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

    private sealed record PendingCommit(ProjectState Project, DirtySet Dirty, SKNode Scene, string? SourcePath);

    private async Task TickOnceAsync(bool forceFullSnapshot, TimeSpan? timeout = null)
    {
        // Non-blocking try-acquire for periodic ticks; bounded wait for forced.
        var acquired = await _commitLock.WaitAsync(
            timeout ?? (forceFullSnapshot ? TimeSpan.FromSeconds(5) : TimeSpan.Zero))
            .ConfigureAwait(false);
        if (!acquired) return;

        try
        {
            // 1. Drain per-project dirty sets on the UI thread (tracker is UI-thread-only).
            var work = await Dispatcher.UIThread.InvokeAsync(() => CollectPendingWork(forceFullSnapshot));
            if (work.Count == 0) return;

            // 2. Snapshot + commit each pending project into its own store.
            var committedAny = false;
            foreach (var item in work)
            {
                SceneSnapshot? snapshot = null;
                try
                {
                    snapshot = await _snapshotProvider.TakeAsync(item.Scene, item.Dirty, item.SourcePath)
                        .ConfigureAwait(false);
                    if (snapshot is null) continue;

                    var store = await GetOrCreateStoreAsync(item.Project).ConfigureAwait(false);
                    await Task.Run(() => store.CommitAsync(snapshot, _cts.Token), _cts.Token)
                        .ConfigureAwait(false);
                    committedAny = true;
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex);
                    // If the commit failed, push the dirty cells back so we retry next tick.
                    try
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => _tracker.Reapply(item.Project.Id, item.Dirty));
                    }
                    catch { /* shutdown */ }
                }
                finally
                {
                    snapshot?.Dispose();
                }
            }

            // 3. Keep workspace.json in sync with what is now on disk.
            if (committedAny)
            {
                var manifest = await Dispatcher.UIThread.InvokeAsync(BuildWorkspaceManifest);
                await WriteWorkspaceManifestAsync(manifest).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _commitLock.Release();
        }
    }

    /// <summary>UI thread only. Drains every project that has pending changes.</summary>
    private List<PendingCommit> CollectPendingWork(bool forceFullSnapshot)
    {
        var result = new List<PendingCommit>();

        foreach (var project in GetProjectsToSave())
        {
            if (project.SceneNode is null) continue;

            // A project with no session store yet must be fully committed once so it can be
            // restored on the next launch — even if the user never touched it after opening.
            if (forceFullSnapshot || !_stores.ContainsKey(project.Id))
                _tracker.MarkAllDirty(project.Id);

            var dirty = _tracker.Drain(project.Id);
            if (dirty.IsEmpty) continue;

            result.Add(new PendingCommit(project, dirty, project.SceneNode, project.File?.Path));
        }

        return result;
    }

    /// <summary>
    /// All open projects; falls back to the bare CurrentProject for flows where the tab
    /// list has not been populated yet (e.g. very early single-project recovery).
    /// </summary>
    private List<ProjectState> GetProjectsToSave()
    {
        var projects = _appState.LoadedProjects.ToList();
        if (projects.Count == 0)
            projects.Add(_appState.CurrentProject);
        return projects;
    }

    // ─────────────── per-project stores ───────────────

    private async Task<IIncrementalSessionStore> GetOrCreateStoreAsync(ProjectState project)
    {
        if (_stores.TryGetValue(project.Id, out var existing))
            return existing;

        var store = new IncrementalSessionStore(_sessionsRoot, project.Id.ToString("N"));
        await store.InitializeAsync(_cts.Token).ConfigureAwait(false);
        _stores[project.Id] = store;
        return store;
    }

    /// <summary>
    /// Synchronous variant for the lifecycle save path. Store initialization is pure file
    /// I/O (no dispatcher round-trips), so a bounded block on the UI thread is safe.
    /// </summary>
    private IIncrementalSessionStore GetOrCreateStoreSync(ProjectState project)
        => _stores.TryGetValue(project.Id, out var existing)
            ? existing
            : GetOrCreateStoreAsync(project).GetAwaiter().GetResult();

    // ─────────────── workspace manifest ───────────────

    /// <summary>UI thread only — reads AppState. Only tabs that own a store are listed.</summary>
    private WorkspaceManifest BuildWorkspaceManifest()
    {
        var tabs = new List<WorkspaceTab>();
        var activeIndex = 0;

        foreach (var project in _appState.LoadedProjects)
        {
            if (!_stores.TryGetValue(project.Id, out var store)) continue;

            if (ReferenceEquals(project, _appState.CurrentProject))
                activeIndex = tabs.Count;

            tabs.Add(new WorkspaceTab
            {
                SessionId = store.SessionId,
                SourceProjectPath = project.File?.Path,
                HasUnsavedChanges = project.HasUnsavedChanges,
            });
        }

        return new WorkspaceManifest
        {
            Tabs = tabs,
            ActiveIndex = activeIndex,
            SavedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Runs on the UI thread (messenger handler); the file write goes to a worker.</summary>
    private void RequestWorkspaceManifestUpdate()
    {
        if (!_platformStuff.SupportsMultipleProjects) return;

        WorkspaceManifest manifest;
        try
        {
            manifest = BuildWorkspaceManifest();
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
            return;
        }

        _ = Task.Run(() => WriteWorkspaceManifestAsync(manifest));
    }

    /// <summary>
    /// Runs on the UI thread (messenger handler). Kicks a regular tick when any open project has a
    /// scene but no session store yet, so a just-opened tab is persisted (its data + a workspace.json
    /// entry) immediately rather than only on the next periodic tick. The tick itself marks storeless
    /// projects all-dirty, commits them (creating their store) and rewrites the manifest. Fire-and-forget
    /// with a bounded lock wait so it still runs if a periodic tick is in flight; the next periodic tick
    /// is the fallback if it does not.
    /// </summary>
    private void RequestEagerPersistOfNewTabs()
    {
        if (!_platformStuff.SupportsMultipleProjects) return;

        var hasStorelessTab = false;
        foreach (var project in _appState.LoadedProjects)
        {
            if (project.SceneNode is not null && !_stores.ContainsKey(project.Id))
            {
                hasStorelessTab = true;
                break;
            }
        }

        if (!hasStorelessTab) return;

        _ = TickOnceAsync(forceFullSnapshot: false, timeout: TimeSpan.FromSeconds(2));
    }

    private async Task WriteWorkspaceManifestAsync(WorkspaceManifest manifest)
    {
        if (!_platformStuff.SupportsMultipleProjects) return;

        await _manifestLock.WaitAsync().ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_sessionsRoot);
            var path = Path.Combine(_sessionsRoot, WorkspaceFile);
            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, JsonConvert.SerializeObject(manifest, Formatting.Indented))
                .ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _manifestLock.Release();
        }
    }

    private WorkspaceManifest? TryReadWorkspaceManifest()
    {
        try
        {
            var path = Path.Combine(_sessionsRoot, WorkspaceFile);
            if (!File.Exists(path)) return null;
            return JsonConvert.DeserializeObject<WorkspaceManifest>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveSessionsRoot(IPlatformStuffService platformStuff)
    {
        return Path.Combine(platformStuff.GetAppFolderPath(), "Sessions");
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync().ConfigureAwait(false); } catch { /* ignore */ }

        // Release the FileShare.None locks so the next process can claim the folders via
        // the normal recovery path. We do NOT delete them — they ARE the saved workspace.
        foreach (var store in _stores.Values)
        {
            try { await store.DisposeAsync(deleteFolder: false).ConfigureAwait(false); }
            catch { /* ignore */ }
        }
        _stores.Clear();

        _commitLock.Dispose();
        _manifestLock.Dispose();
        _cts.Dispose();
    }
}
