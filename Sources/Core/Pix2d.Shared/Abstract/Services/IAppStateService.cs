namespace Pix2d.Abstract.Services
{
    public interface AppStateService<out TAppState> : AppStateService
    {
        TAppState AppState { get; }
    }

    public interface AppStateService
    {
        void SwitchToFullMode();

    }
}