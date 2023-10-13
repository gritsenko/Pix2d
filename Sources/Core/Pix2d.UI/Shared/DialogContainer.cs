using System.Threading.Tasks;
using CommonServiceLocator;
using Mvvm;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class DialogContainer : ViewBase, IDialogContainer
{
    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<DialogContainer, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<DialogContainer, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = default;

    public Control Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    public static readonly DirectProperty<DialogContainer, string> TitleProperty
        = AvaloniaProperty.RegisterDirect<DialogContainer, string>(nameof(Title), o => o.Title, (o, v) => o.Title = v);

    private string _title = "Dialog";

    public string Title
    {
        get => _title;
        set => SetAndRaise(TitleProperty, ref _title, value);
    }

    protected override object Build() =>
        new Border()
            .IsVisible(false)
            .Ref(out _ovarlayBorder)
            .Background(StaticResources.Brushes.ModalOverlayBrush)
            .Child(
                new PopupView()
                    .MinWidth(300)
                    .MinHeight(150)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Ref(out _contentControl)
                    .Header(Title, BindingMode.OneWay, bindingSource: this)
                    .IsOpen(true)
                    .CloseButtonCommand(new RelayCommand(OnCloseButtonClicked))
            );

    private PopupView _contentControl;
    private Border _ovarlayBorder;
    private TaskCompletionSource<bool?> _cts;

    protected override void OnAfterInitialized()
    {
        var dialogService = ServiceLocator.Current.GetInstance<IDialogService>();
        dialogService.SetDialogContainer(this);
    }

    public Task ShowDialogAsync(IDialogView dialog)
    {
        _cts = new TaskCompletionSource<bool?>();
        if (dialog is ViewBase control)
        {
            Title = dialog.Title;
            _contentControl.Content = control;
            SetVisible(true);

            dialog.OnDialogClosed = b =>
            {
                _cts.SetResult(b);
                SetVisible(false);
                _contentControl.Content = null;
            };

            return _cts.Task;
        }

        throw new Exception("dialog is not control");
    }

    private void OnCloseButtonClicked()
    {
        _cts.SetResult(null);
        SetVisible(false);
    }

    private void SetVisible(bool isVisible)
    {
        _contentControl.IsOpen = isVisible;
        _ovarlayBorder.IsVisible = isVisible;
    }
}