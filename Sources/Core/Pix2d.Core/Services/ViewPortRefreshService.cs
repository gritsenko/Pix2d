using Pix2d.Messages;

namespace Pix2d.Services;

public class ViewPortRefreshService : IViewPortRefreshService
{
    private readonly IViewPortService _viewPortService;
    private readonly IMessenger _messenger;
    private readonly AppState _appState;

    public ViewPortRefreshService(IViewPortService viewPortService, IMessenger messenger, AppState appState)
    {
        _viewPortService = viewPortService;
        _messenger = messenger;
        _appState = appState;
        InitializeGlobalRefreshEvents();
    }

    private void InitializeGlobalRefreshEvents()
    {
        _messenger.Register<ProjectLoadedMessage>(this, m => Refresh());
        _messenger.Register<OperationInvokedMessage>(this, m => Refresh());

        _appState.CurrentProject.ViewPortState.WatchFor(x => x.ShowGrid, Refresh);
        _appState.CurrentProject.ViewPortState.WatchFor(x => x.GridSpacing, Refresh);

        //CoreServices.SnappingService.GridToggled += (sender, args) => Refresh();
        //CoreServices.SnappingService.GridCellSizeChanged += (sender, args) => Refresh();
    }


    public void Refresh()
    {
        _viewPortService.ViewPort?.Refresh();
    }

}