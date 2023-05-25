using Pix2d.Mvvm;

namespace Pix2d.ViewModels.ToolSettings;

public class ObjectManipulateToolViewModel : ToolSettingsBaseViewModel
{
    public IMessenger Messenger { get; }
    public AppState AppState { get; }
    public IViewModelService ViewModelService { get; }

    public ObjectManipulateToolViewModel(IMessenger messenger, AppState appState, IViewModelService viewModelService)
    {
        Messenger = messenger;
        AppState = appState;
        ViewModelService = viewModelService; 
        //Messenger.Register<NodesSelectedMessage>(this, m => LoadData());
    }
}