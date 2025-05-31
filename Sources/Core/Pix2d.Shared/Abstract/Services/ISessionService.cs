namespace Pix2d.Abstract.Services;

public interface ISessionService
{
    Task TrySaveSessionAsync();
        
    Task TryLoadSessionAsync();
}