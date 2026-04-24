using Mvvm;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class DialogContainer : ViewBase, IDialogContainer
{
    public DialogContainer(IDialogService dialogService)
    {
        dialogService.SetDialogContainer(this);
    }

    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<DialogContainer, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<DialogContainer, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = null!;

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
                ViewFactory.Create<PopupView>()
                    .MinWidth(300)
                    .MinHeight(150)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Ref(out _contentControl)
                    .Header(string.Empty)
                    .IsOpen(true)
                    .OnCloseButtonClicked(e => OnCloseButtonClicked())
            );

    private PopupView _contentControl = null!;
    private Border _ovarlayBorder = null!;

    public event EventHandler? CloseButtonClicked;

    public void ShowDialog(IDialogView dialog)
    {
        if (dialog is not Control control)
            throw new Exception("dialog is not control");

        Title = dialog.Title;
        _contentControl.Header = Title;
        _contentControl.Content = control;
        SetVisible(true);
    }

    public void CloseDialog()
    {
        SetVisible(false);
        Title = "";
        _contentControl.Header = string.Empty;
        _contentControl.Content = default!;
    }

    private void OnCloseButtonClicked()
    {
        CloseButtonClicked?.Invoke(this, EventArgs.Empty);
    }

    private void SetVisible(bool isVisible)
    {
        _contentControl.IsOpen = isVisible;
        _ovarlayBorder.IsVisible = isVisible;
    }
}