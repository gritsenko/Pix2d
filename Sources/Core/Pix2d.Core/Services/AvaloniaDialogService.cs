#nullable enable
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Pix2d.Abstract.UI;
using Pix2d.UI.Dialogs;

namespace Pix2d.Services;

public class AvaloniaDialogService : IDialogService
{
    private Window? _window;
    private IDialogContainer? _dialogContainer;
    private Canvas? _panelsContainer;
    public Window? Window => _window ??= global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;

    private readonly Stack<IDialogInfo> _openedDialogStack = new();
    private IDialogInfo? _currentDialog;
    void IDialogService.SetDialogContainer(object container)
    {
        _dialogContainer = container as IDialogContainer;

        if (_dialogContainer == null)
            throw new NullReferenceException("Dialog container is not set!");

        _dialogContainer.CloseButtonClicked += _dialogContainer_CloseButtonClicked;
    }

    private void _dialogContainer_CloseButtonClicked(object? sender, EventArgs e)
    {
        if (_dialogContainer == null)
            throw new NullReferenceException("Dialog container is not set!");

        _dialogContainer.CloseDialog();

        if (_currentDialog != null)
            _currentDialog.Cancel();

        if (_openedDialogStack.Any())
        {
            _currentDialog = _openedDialogStack.Pop();
        }

        if (_currentDialog != null)
            _dialogContainer.ShowDialog(_currentDialog.DialogView);
    }

    void IDialogService.SetPanelsContainer(object container)
    {
        _panelsContainer = container as Canvas;

        if (_panelsContainer == null)
            throw new NullReferenceException("Panels container is not set!");

    }

    public void Alert(string message, string title)
    {
        Dispatcher.UIThread.InvokeAsync(() => ShowAlert(message, title));
    }

    public async Task ShowAlert(string message, string title)
    {
        var dialog = new AlertDialog { Message = message, Title = title };
        await ShowDialog(dialog);
    }

    public async Task<string?> ShowInputDialogAsync(string message, string title, string defaultValue = "")
    {
        var dialog = new InputDialogView() { Title = title, Message = message, DialogResult = defaultValue };
        return await ShowDialog(dialog);
    }

    public async Task<bool> ShowYesNoDialog(string message, string title, string okLabel, string cancelLabel)
    {
        var dialog = new YesNoDialogView { Title = title, Message = message, OkLabel = okLabel, CancelLabel = cancelLabel };
        return await ShowDialog(dialog);
    }

    public Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog()
    {
        return ShowDialog(new UnsavedChangesDialogView());
    }

    public void ShowPanelView(IToolPanel panel)
    {
        if (_panelsContainer == null)
            throw new NullReferenceException("Panel container is not set!");

        if (panel is Control panelControl)
        {
            if (!_panelsContainer.Children.Contains(panelControl))
                _panelsContainer.Children.Add(panelControl);
        }
    }

    public void TogglePanelView(IToolPanel panel)
    {
        if (_panelsContainer == null)
            throw new NullReferenceException("Panel container is not set!");

        if (panel is Control panelControl)
        {
            if (!_panelsContainer.Children.Contains(panelControl))
                _panelsContainer.Children.Add(panelControl);
            else
                _panelsContainer.Children.Remove(panelControl);
        }
    }

    private Task<TResult> ShowDialog<TResult>(IDialogView<TResult> dialog)
    {
        if (_dialogContainer == null)
            throw new NullReferenceException("Dialog container is not set!");

        if (_currentDialog != null)
            _openedDialogStack.Push(_currentDialog);

        var dialogInfo = new OpenedDialogInfo<TResult>(dialog, () =>
        {
            if (_openedDialogStack.Count == 0)
            {
                _dialogContainer.CloseDialog();
                _currentDialog = null;
            }
            else
            {
                _currentDialog = _openedDialogStack.Pop();
                _dialogContainer.ShowDialog(_currentDialog.DialogView);
            }
        });

        _currentDialog = dialogInfo;
        _dialogContainer.ShowDialog(_currentDialog.DialogView);

        return dialogInfo.Task;
    }


    interface IDialogInfo
    {
        void Cancel();
        IDialogView DialogView { get; }
    }
    private class OpenedDialogInfo<TResult> : IDialogInfo
    {
        private readonly Action _onCloseAction;
        private readonly TaskCompletionSource<TResult> _cts = new();

        public OpenedDialogInfo(IDialogView<TResult> dialogView, Action onCloseAction)
        {
            _onCloseAction = onCloseAction;
            DialogView = dialogView;

            dialogView.OnDialogClosed = b =>
            {
                _onCloseAction();
                _cts.SetResult(dialogView.DialogResult);
            };
        }

        public Task<TResult> Task => _cts.Task;

        public IDialogView DialogView { get; }

        public void Cancel()
        {
            _cts.SetResult(default);
            _onCloseAction();
        }
    }
}