using Mvvm;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class DialogContainer : ComponentBase, IDialogContainer
{
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
                new PopupView()
                    .MinWidth(300)
                    .MinHeight(150)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Ref(out _contentControl)
                    .Header(Title, BindingMode.OneWay, bindingSource: this)
                    .IsOpen(true)
                    .OnCloseButtonClicked(_ => OnCloseButtonClicked())
            );

    private PopupView _contentControl;
    private Border _ovarlayBorder;

    public event EventHandler CloseButtonClicked;

    [Inject] private IDialogService DialogService { get; set; } = null!;

    protected override void OnAfterInitialized()
    {
        DialogService.SetDialogContainer(this);
    }

    public void ShowDialog(IDialogView dialog)
    {
        if (dialog is not ViewBase control)
            throw new Exception("dialog is not control");

        Title = dialog.Title;
        _contentControl.Content = control;
        SetVisible(true);
    }

    public void CloseDialog()
    {
        SetVisible(false);
        Title = "";
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