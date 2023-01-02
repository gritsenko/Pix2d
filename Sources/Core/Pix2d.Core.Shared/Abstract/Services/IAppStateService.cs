namespace Pix2d.Abstract
{
    public interface IAppStateService<out TAppState> : IAppStateService
    {
        TAppState AppState { get; }
    }

    public interface IAppStateService
    {
        void SwitchToFullMode();

    }
}