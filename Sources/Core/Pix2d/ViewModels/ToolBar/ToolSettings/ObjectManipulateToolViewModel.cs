using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.ViewModels.NodeProperties;

namespace Pix2d.ViewModels.ToolSettings
{
    public class ObjectManipulateToolViewModel : ToolSettingsBaseViewModel
    {
        public IMessenger Messenger { get; }
        public AppState AppState { get; }
        public IViewModelService ViewModelService { get; }

        public SelectionPropertiesViewModel SelectionProperties => ViewModelService.GetViewModel<SelectionPropertiesViewModel>();

        public ObjectManipulateToolViewModel(IMessenger messenger, AppState appState, IViewModelService viewModelService)
        {
            Messenger = messenger;
            AppState = appState;
            ViewModelService = viewModelService;
            Messenger.Register<NodesSelectedMessage>(this, m => LoadData());
            LoadData();
        }

        private void LoadData()
        {
            SelectionProperties.UpdateSelection(AppState.CurrentProject.Selection);
        }

    }
}