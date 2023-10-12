using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Platform.FileSystem;
using Pix2d.Messages;
using Pix2d.Primitives;

namespace Pix2d.Services;

public class SessionService : ISessionService
{
    private const string SessionFileName = "SessionProject4";

    private AppState AppState { get; }

    private ProjectState ProjectState => AppState.CurrentProject;

    public IProjectService ProjectService { get; }
    public IFileService FileService { get; }
    public ISettingsService SettingsService { get; }
    public IMessenger Messenger { get; }
    private Timer _sessionTimer;

    private bool _isSavingSession;

    public SessionService(IProjectService projectService, AppState appState, IFileService fileService, ISettingsService settingsService, IMessenger messenger)
    {
        ProjectService = projectService;
        FileService = fileService;
        SettingsService = settingsService;
        Messenger = messenger;
        AppState = appState as AppState;

        Messenger.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
        StartAutoSave();
    }

    private void OnProjectLoaded(ProjectLoadedMessage message)
    {
        if (!message.LoadedFromLocalSession)
        {
            ClearSessionInfo();
        }

        StartAutoSave();
    }

    public void StartAutoSave()
    {
        Debug.WriteLine("Resetting session timer");
        var period = AppState.Settings.AutoSaveInterval;
        if (_sessionTimer == null)
            _sessionTimer = new Timer(OnSessionTimerTick, this, period, period);
        else
            _sessionTimer.Change(period, period);
    }

    public void StopAutoSave()
    {
        _sessionTimer?.Change(-1, -1);
    }

    private async void OnSessionTimerTick(object state)
    {
        OpLog();

        if (_isSavingSession || AppState.IsBusy)
            return;

        await SaveSessionAsync();
    }


    public async Task SaveSessionAsync()
    {
        if (_isSavingSession)
        {
            //skipping saving
            return;
        }

        try
        {
            var hasChanges = ProjectState.HasUnsavedChanges;
            var lastSessionInfo = ProjectState.LastSessionInfo;

            if (!hasChanges && lastSessionInfo?.ProjectPath == ProjectState.File?.Path)
            {
                Debug.WriteLine("No changes");
                return;
            }

            OpLog();
            _isSavingSession = true;

            var sessionInfo = new SessionInfo
            {
                ProjectPath = ProjectState.File?.Path ?? lastSessionInfo?.ProjectPath,
                LoadFromSessionFolder = hasChanges
            };

            if (hasChanges)
            {
                var file = await GetSessionFileToWrite();
                await ProjectService.SaveCurrentProjectToFileAsync(file, true);
            }

            SettingsService.Set("session", sessionInfo);
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }
        finally
        {
            _isSavingSession = false;
        }
    }

    public static void OpLog(string info = null, [CallerMemberName] string caller = null)
    {
        SessionLogger.OpLog(info, caller);
    }

    public async Task<bool> TryLoadSessionAsync()
    {
        try
        {
            if (SettingsService.TryGet<SessionInfo>("session", out var sessionInfo) && sessionInfo != null)
            {
                ProjectState.LastSessionInfo = sessionInfo;

                var localSessionFile = sessionInfo.LoadFromSessionFolder;

                var file = localSessionFile
                    ? await GetSessionFileToRead()
                    : await FileService.GetFileContentSourceAsync(sessionInfo.ProjectPath);

                var success = await ProjectService.OpenFilesAsync(new[] {file}, localSessionFile);
                return success;
            }
        }
        catch (FileNotFoundException fex)
        {
            //session can't be loaded
        }
        catch (Exception ex)
        {
            Logger.LogException(ex);
        }

        return false;
    }

    private Task<IWriteDestinationFolder> GetSessionFolder()
        => FileService.GetLocalFolderAsync("Sessions");


    private async Task<IFileContentSource> GetSessionFileToRead()
    {
        var folder = await GetSessionFolder();
        return await folder.GetFileSourceToReadAsync(SessionFileName, "pix2d");
    }
    private async Task<IFileContentSource> GetSessionFileToWrite()
    {
        var folder = await GetSessionFolder();
        return await folder.GetFileSourceAsync(SessionFileName, "pix2d", true);
    }

    private void ClearSessionInfo()
    {
        SettingsService.Set<SessionInfo>("session", null);
        ProjectState.LastSessionInfo = null;
    }


}