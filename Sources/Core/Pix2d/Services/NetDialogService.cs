using System;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.UI;
using Pix2d.Views.Dialogs;

//using MahApps.Metro.Controls;

namespace Pix2d.Services
{
    public class AvaloniaDialogService : IDialogService
    {
        private Window _window;
        private IDialogContainer _dialogContainer;
        public Window Window => _window ?? (_window = global::Avalonia.Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);

        void IDialogService.SetDialogContainer(object container)
        {
            _dialogContainer = container as IDialogContainer;
        }

        public void Alert(string message, string title)
        {
            //if (Window == null)
            //{
            //    MessageBox.Show(message, title);
            //}
            //else
            //{
            //    //await Window.ShowMessageAsync(title, message);
            //}

        }
        public void Toast(string message, string title, ToastType toastType = ToastType.Info)
        {
            //switch (toastType)
            //{
            //    case ToastType.Info:
            //        InfoToaster.Toast(Window, title, message, ToasterPosition.PrimaryScreenTopRight,
            //            ToasterAnimation.SlideInFromRight, 8);
            //        break;
            //    case ToastType.Error:
            //        ErrorToaster.Toast(Window, title, message, ToasterPosition.PrimaryScreenTopRight,
            //            ToasterAnimation.SlideInFromRight, 8);
            //        break;
            //    case ToastType.Success:
            //        SuccessToaster.Toast(Window, title, message, ToasterPosition.PrimaryScreenTopRight,
            //            ToasterAnimation.SlideInFromRight, 8);
            //        break;
            //    case ToastType.Warning:
            //        WarningToaster.Toast(Window, title, message, ToasterPosition.PrimaryScreenTopRight,
            //            ToasterAnimation.SlideInFromRight, 8);
            //        break;
            //}
        }

        public Task<string> ShowInputDialogAsync(string message, string title, string defaultValue = "")
        {
            return Task.FromResult("");
            //if (Window == null)
            //{
            //    //todo: �����������
            //    return null;
            //}
            //else
            //{
            //var settings = new MetroDialogSettings
            //{
            //    DefaultText = defaultValue,
            //    AffirmativeButtonText = "Ok",
            //    NegativeButtonText = "Cancel"
            //};
            //var result = await Window.ShowInputAsync(title, message, settings);
            //return result;
            //}
        }

        async Task<bool> IDialogService.ShowYesNoDialog(string message, string title, string okLabel, string cancelLabel)
        {
            if (_dialogContainer == null) return false;
            var dialog = new YesNoDialogView { Title = title, Message = message, OkLabel = okLabel, CancelLabel = cancelLabel };
            await _dialogContainer.ShowDialogAsync(dialog);
            return dialog.DialogResult;
        }

        public async Task<bool> ShowAlert(string message, string title)
        {
            if (_dialogContainer == null) return true;
            await _dialogContainer.ShowDialogAsync(new AlertDialog());
            return true;
        }

        public async Task<UnsavedChangesDialogResult> ShowUnsavedChangesInProjectDialog()
        {
            if (_dialogContainer == null) return UnsavedChangesDialogResult.No;
            var dialog = new UnsavedChangesDialogView();
            await _dialogContainer.ShowDialogAsync(dialog);
            return dialog.DialogResult;
        }

        public void ShowAboutDialog()
        {
            //if (Window != null)
            //{
            //    var aboutWnd = new AboutWindow();
            //    aboutWnd.Owner = System.Windows.Window.GetWindow(Window);
            //    aboutWnd.ShowDialog();
            //}
        }

        public void ShowGridStapDialog()
        {
            //if (Window != null)
            //{
            //    var customSnapToGridDialog = new SnapToGridDialog();
            //    customSnapToGridDialog.Owner = System.Windows.Window.GetWindow(Window);
            //    customSnapToGridDialog.ShowDialog();
            //}
            //Messenger.Default.Send(new OpenCustomSnapToGridDialogMessage());
        }

        public Task<bool?> ShowDialogAsync(IDialogView dlg)
        {
            throw new NotImplementedException();
        }
    }
}