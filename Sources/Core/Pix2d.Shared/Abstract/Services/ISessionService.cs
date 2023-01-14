using System.Threading.Tasks;

namespace Pix2d.Abstract
{
    public interface ISessionService
    {
        Task SaveSessionAsync();
        
        Task<bool> TryLoadSessionAsync();
        void StopAutoSave();
        void StartAutoSave();
    }
}