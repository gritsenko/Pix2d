using System.Windows.Input;
using Mvvm.Messaging;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite;

namespace Pix2d.ViewModels
{
    public class TopBarViewModel : Pix2dViewModelBase
    {
        public IOperationService OperationService { get; }
        public IMessenger Messenger { get; }
        private readonly IMenuController _menuController;
        private readonly IPanelsController _panelsController;
        public int UndoSteps => OperationService?.UndoOperationsCount ?? 0;

        public ICommand ToggleMenuCommand => GetCommand(() => _menuController.ShowMenu = !_menuController.ShowMenu);
        public ICommand ToggleExtraToolsCommand => GetCommand(() =>
            {
                _menuController.ShowExtraTools = !_menuController.ShowExtraTools;
                IsExtraToolsVisible = _menuController.ShowExtraTools;
             });
        public ICommand ToggleTimelineCommand => GetCommand(() =>
            {
                _menuController.ShowTimeline = !_menuController.ShowTimeline;
                OnPropertyChanged(nameof(IsTimelineVisible));
            });
        public ICommand ToggleAssetLibraryCommand => GetCommand(() =>
            {
                if (!_menuController.ShowSidebar)
                {
                    _menuController.ShowSidebar = true;
                }
                _menuController.ShowAssetsLibrary = !_menuController.ShowAssetsLibrary;
                OnPropertyChanged(nameof(IsAssetLibraryVisible));
            });

        public ICommand ClearLayerCommand => MapCommand(SpritePlugin.EditCommands.Clear);
        public ICommand ToggleGridCommand => MapCommand(Commands.View.Snapping.ToggleGrid);
        public ICommand PasteCommand => MapCommand(Commands.Edit.Clipboard.TryPaste);
        public ICommand CopyCommand => MapCommand(Commands.Edit.Clipboard.Copy);
        public ICommand CutCommand => MapCommand(Commands.Edit.Clipboard.Cut);
        public ICommand UndoCommand => MapCommand(Commands.Edit.Undo); //todo: implement can execute change
        public ICommand RedoCommand => MapCommand(Commands.Edit.Redo); //todo: implement can execute change

        public ICommand ShowExportDialogCommand => GetCommand(() => _menuController.ShowExportDialog = !_menuController.ShowExportDialog);
        public ICommand ToggleLayersCommand => GetCommand(() => _panelsController.ShowLayers = !_panelsController.ShowLayers);
        public TopBarViewModel(IMenuController menuController, IPanelsController panelsController, IOperationService operationService, IMessenger messenger)
        {
            OperationService = operationService;
            Messenger = messenger;
            _menuController = menuController;
            _panelsController = panelsController;

            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        }

        private void OnOperationInvoked(OperationInvokedMessage obj)
        {
            OnPropertyChanged(nameof(UndoSteps));
        }

        public bool IsExtraToolsVisible
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool IsTimelineVisible => _menuController.ShowTimeline;
        public bool IsAssetLibraryVisible => _menuController.ShowAssetsLibrary;
    }
}