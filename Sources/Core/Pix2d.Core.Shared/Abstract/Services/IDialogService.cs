using System.Threading.Tasks;
using Pix2d.Abstract.UI;

namespace Pix2d.Abstract.Platform
{
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
        void Alert(string message, string title);

        void Toast(string message, string title, ToastType toastType = ToastType.Info);

        Task<string> ShowInputDialogAsync(string message, string title, string defaultValue = "");

        Task<bool> ShowYesNoDialog(string message, string title, string okLabel = "Ok", string cancelLabel = "Cancel");
        Task<bool> ShowAlert(string message, string title);

        Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog();

        void ShowAboutDialog();

        void ShowGridStapDialog();
        Task<bool?> ShowDialogAsync(IDialogView dialog);
    }
}