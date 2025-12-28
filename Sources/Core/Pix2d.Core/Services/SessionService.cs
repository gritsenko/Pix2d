#nullable enable
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Primitives;
using Pix2d.Project;
using System.Diagnostics;

namespace Pix2d.Services;

public sealed class SessionService(
    ISessionProjectLoader sessionProjectLoader,
    AppState appState,
    IFileService fileService,
    ISettingsService settingsService)
    : ISessionService, IAsyncDisposable
{
    private const string SessionFileName = "SessionProject4";
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private PeriodicTimer? _autoSaveTimer;
    private Task? _autoSaveTask;
    private DateTime _lastSaveTime = DateTime.MinValue;

    private ProjectState ProjectState => appState.CurrentProject;
    private IWriteDestinationFolder? _sessionFolder;

    public async ValueTask DisposeAsync()
    {
        StopAutoSave();

        await _cts.CancelAsync();

        if (_autoSaveTask is not null)
        {
            await _autoSaveTask;
        }

        _cts.Dispose();
        _saveLock.Dispose();
    }

    public void StartAutoSave()
    {
        StopAutoSave();

        var period = appState.Settings.AutoSaveInterval;
        _autoSaveTimer = new PeriodicTimer(period);
        _autoSaveTask = RunAutoSaveLoopAsync(_autoSaveTimer);
    }

    private void StopAutoSave()
    {
        _autoSaveTimer?.Dispose();
        _autoSaveTimer = null;
        _autoSaveTask = null;
    }

    private async Task RunAutoSaveLoopAsync(PeriodicTimer timer)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                await TrySaveSessionAsync();
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (ObjectDisposedException) { /* Timer disposed during restart/shutdown */ }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public async Task TrySaveSessionAsync(bool criticalSave = false)
    {
        if (!await _saveLock.WaitAsync(criticalSave ? TimeSpan.FromSeconds(5) : TimeSpan.Zero))
        {
            if (criticalSave)
            {
                Logger.Log($"Critical save blocked: app busy or lock held");
            }
            return;
        }

        try
        {
            await TrySaveSessionAsyncInternal(forceWriteToSessionFolder: false);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task TryLoadSessionAsync()
    {
        if (OperatingSystem.IsBrowser())
        {
            Logger.Log("Session loading is not supported in browsers");
            return;
        }

        try
        {
            if (settingsService.TryGet<SessionInfo>("session", out var sessionInfo) && sessionInfo is not null)
            {
                ProjectState.LastSessionInfo = sessionInfo;

                var file = sessionInfo.LoadFromSessionFolder
                    ? await GetSessionFileToReadAsync()
                    : await fileService.GetFileContentSourceAsync(sessionInfo.ProjectPath);

                await sessionProjectLoader.OpenProjectFromSessionAsync(file);
                ClearSessionInfo();
            }
        }
        catch (FileNotFoundException fex)
        {
            Logger.LogException(fex);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            StartAutoSave();
        }
    }

    private async ValueTask<IWriteDestinationFolder> GetSessionFolderAsync() =>
        _sessionFolder ??= await fileService.GetLocalFolderAsync("Sessions");

    private async Task<IFileContentSource> GetSessionFileToReadAsync()
    {
        var folder = await GetSessionFolderAsync();
        return await folder.GetFileSourceToReadAsync(SessionFileName, "pix2d");
    }

    private async Task<IFileContentSource> GetSessionFileToWriteAsync()
    {
        var folder = await GetSessionFolderAsync();
        return await folder.GetFileSourceAsync(SessionFileName, "pix2d", overwrite: true);
    }

    private void ClearSessionInfo()
    {
        settingsService.Set<SessionInfo>("session", null);
        ProjectState.LastSessionInfo = null;
    }

    public async Task ForceSaveAsync(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);

        try
        {
            await _saveLock.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Log("Force save timed out after " + timeout.TotalSeconds + " seconds");
            return;
        }

        try
        {
            await TrySaveSessionAsyncInternal(forceWriteToSessionFolder: true);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private async Task TrySaveSessionAsyncInternal(bool forceWriteToSessionFolder)
    {
        if (!ProjectState.HasUnsavedChanges &&
            ProjectState.LastSessionInfo?.ProjectPath == ProjectState.File?.Path &&
            !forceWriteToSessionFolder)
        {
            Debug.WriteLine("No changes");
            return;
        }

        SessionLogger.OpLog(forceWriteToSessionFolder ? "Force saving session" : "Saving session");

        var sessionInfo = new SessionInfo
        {
            ProjectPath = ProjectState.File?.Path ?? ProjectState.LastSessionInfo?.ProjectPath,
            LoadFromSessionFolder = forceWriteToSessionFolder || ProjectState.HasUnsavedChanges
        };

        if (sessionInfo.LoadFromSessionFolder)
        {
            var file = await GetSessionFileToWriteAsync();
            await ProjectPacker.WriteProjectAsync(file, appState.CurrentProject.SceneNode);
        }

        settingsService.Set("session", sessionInfo);
        _lastSaveTime = DateTime.UtcNow;
    }
}