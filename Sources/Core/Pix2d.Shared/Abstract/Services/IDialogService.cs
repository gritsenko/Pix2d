#nullable enable
using System.Threading.Tasks;
using Pix2d.Abstract.UI;

namespace Pix2d.Abstract.Services;

public enum ToastType
{
    Info,
    Success,
    Warning,
    Error,
}

public enum UnsavedChangesDialogResult
{
    Yes,
    No,
    Cancel
}


public interface IDialogService
{
    void SetDialogContainer(object container);
    void SetPanelsContainer(object container);
    void Alert(string message, string title);
    Task ShowAlert(string message, string title);

    Task<string?> ShowInputDialogAsync(string message, string title, string defaultValue = "");

    Task<bool> ShowYesNoDialog(string message, string title, string okLabel = "Ok", string cancelLabel = "Cancel");

    Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog();
    void ShowPanelView(IToolPanel panel);
    void TogglePanelView(IToolPanel panel);
}