namespace Pix2d.Abstract.Services;

public interface ISessionService
{
    Task TrySaveSessionAsync(bool criticalSave = false);
        
    Task TryLoadSessionAsync();

    Task ForceSaveAsync(TimeSpan timeout);
}