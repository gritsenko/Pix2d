using Avalonia.LogicalTree;
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

        // The view may still be parented in a previously-shown (possibly stale) container. Detach it
        // first, otherwise assigning it as our content throws "already has a visual parent".
        DetachFromCurrentParent(control);

        Title = dialog.Title;
        _contentControl.Header = Title;
        _contentControl.Content = control;
        SetVisible(true);
    }

    private static void DetachFromCurrentParent(Control control)
    {
        // Dialog views are hosted through a PopupView's Content; clear that source so the OneWay
        // binding releases the view from the old presenter before we re-host it here.
        if (control.FindLogicalAncestorOfType<PopupView>() is { } ownerPopup && ReferenceEquals(ownerPopup.Content, control))
            ownerPopup.Content = null;
        else switch (control.Parent)
        {
            case ContentControl cc when ReferenceEquals(cc.Content, control):
                cc.Content = null;
                break;
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator dec when ReferenceEquals(dec.Child, control):
                dec.Child = null;
                break;
        }
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