using System.Windows.Input;
using Mvvm.Messaging;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.UI;
using Pix2d.Messages;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels;

public class TopBarViewModel : Pix2dViewModelBase
{
    public IOperationService OperationService { get; }
    public IMessenger Messenger { get; }
    private readonly IMenuController _menuController;
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

    public ICommand ShowExportDialogCommand => GetCommand(() => _menuController.ShowExportDialog = !_menuController.ShowExportDialog);
    public TopBarViewModel(IMenuController menuController, IOperationService operationService, IMessenger messenger)
    {
        OperationService = operationService;
        Messenger = messenger;
        _menuController = menuController;

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
}