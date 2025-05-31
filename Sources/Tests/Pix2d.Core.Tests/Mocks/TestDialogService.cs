using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;

namespace Pix2d.Core.Tests.Mocks;

public class TestDialogService : IDialogService
{
    private string _nextInput;

    public void SetDialogContainer(object container)
    {
    }

    public void SetPanelsContainer(object container)
    {
        throw new NotImplementedException();
    }

    public void Alert(string message, string title)
    {
    }

    public void Toast(string message, string title, ToastType toastType = ToastType.Info)
    {
    }

    public Task<string> ShowInputDialogAsync(string message, string title, string defaultValue = "")
    {
        return Task.FromResult(_nextInput);
    }

    public Task<bool> ShowYesNoDialog(string message, string title, string okLabel = "Ok", string cancelLabel = "Cancel")
    {
        throw new NotImplementedException();
    }

    public Task ShowAlert(string message, string title)
    {
        LastAlert = message;
        return Task.FromResult(true);
    }

    public Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog()
    {
        throw new NotImplementedException();
    }

    public void ShowPanelView(IToolPanel panel)
    {
        throw new NotImplementedException();
    }

    public void TogglePanelView(IToolPanel panel)
    {
        throw new NotImplementedException();
    }

    public void ShowAboutDialog()
    {
    }

    public void ShowGridStapDialog()
    {
    }

    public Task<bool?> ShowDialogAsync(IDialogView dialog)
    {
        throw new NotImplementedException();
    }

    public void SetInput(string input)
    {
        _nextInput = input;
    }

    public IEnumerable<char> LastAlert { get; set; }
}