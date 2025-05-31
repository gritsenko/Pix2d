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

    private ProjectState ProjectState => appState.CurrentProject;
    private IWriteDestinationFolder? _sessionFolder;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();

        if (_autoSaveTask is not null)
        {
            await _autoSaveTask;
        }

        _autoSaveTimer?.Dispose();
        _cts.Dispose();
        _saveLock.Dispose();
    }

    public void StartAutoSave()
    {
        var period = appState.Settings.AutoSaveInterval;
        _autoSaveTimer?.Dispose();

        _autoSaveTimer = new PeriodicTimer(period);
        _autoSaveTask = RunAutoSaveLoopAsync();
    }

    private async Task RunAutoSaveLoopAsync()
    {
        try
        {
            while (await _autoSaveTimer!.WaitForNextTickAsync(_cts.Token))
            {
                await TrySaveSessionAsync();
            }
        }
        catch (OperationCanceledException) { /* Normal shutdown */ }
        catch (Exception e)
        {
            Logger.LogException(e);
        }
    }

    public async Task TrySaveSessionAsync()
    {
        if (appState.IsBusy || !await _saveLock.WaitAsync(0))
            return;

        try
        {
            if (!ProjectState.HasUnsavedChanges &&
                ProjectState.LastSessionInfo?.ProjectPath == ProjectState.File?.Path)
            {
                Debug.WriteLine("No changes");
                return;
            }

            SessionLogger.OpLog("Saving session");

            var sessionInfo = new SessionInfo
            {
                ProjectPath = ProjectState.File?.Path ?? ProjectState.LastSessionInfo?.ProjectPath,
                LoadFromSessionFolder = ProjectState.HasUnsavedChanges
            };

            if (ProjectState.HasUnsavedChanges)
            {
                var file = await GetSessionFileToWriteAsync();
                await ProjectPacker.WriteProjectAsync(file, appState.CurrentProject.SceneNode);
            }

            settingsService.Set("session", sessionInfo);
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
}