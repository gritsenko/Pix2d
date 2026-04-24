using Avalonia.Styling;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class YesNoDialogView : ViewBase, IDialogView<bool>
{
    public static readonly DirectProperty<YesNoDialogView, string> MessageProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(Message), o => o.Message, (o, v) => o.Message = v);

    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        set => SetAndRaise(MessageProperty, ref _message, value);
    }

    public static readonly DirectProperty<YesNoDialogView, string> OkLabelProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(OkLabel), o => o.OkLabel, (o, v) => o.OkLabel = v);

    private string _okLabel = string.Empty;

    public string OkLabel
    {
        get => _okLabel;
        set => SetAndRaise(OkLabelProperty, ref _okLabel, value);
    }
    
    public static readonly DirectProperty<YesNoDialogView, string> CancelLabelProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(CancelLabel), o => o.CancelLabel, (o, v) => o.CancelLabel = v);

    private string _cancelLabel = string.Empty;

    public string CancelLabel
    {
        get => _cancelLabel;
        set => SetAndRaise(CancelLabelProperty, ref _cancelLabel, value);
    }
    
    protected override object Build() =>
        new Grid()
            .Rows("*,48")
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(8, 0))
                    .Text(this, x => x.Message, BindingMode.OneWay),

                new StackPanel().Row(1)
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Styles(
                        new Style(s => s.OfType(typeof(Button)))
                        {
                            Setters =
                            {
                                new Setter(Button.MarginProperty, new Thickness(8,0)),
                                new Setter(Button.WidthProperty, 100d)
                            }
                        }) //styles

                    .Children(
                        new Button()
                            .Classes("btn")
                            .Content(this, x => x.OkLabel, BindingMode.OneWay)
                            .Background(StaticResources.Brushes.AccentBrush)
                            .OnClick(_ =>
                            {
                                DialogResult = true;
                                OnDialogClosed?.Invoke(true);
                            }),
                        new Button()
                            .Classes("btn")
                            .Content(this, x => x.CancelLabel, BindingMode.OneWay)
                            .OnClick(_ =>
                            {
                                DialogResult = false;
                                OnDialogClosed?.Invoke(false);
                            })
                    ) //stack panel children
            );

    public string Title { get; set; } = string.Empty;
    public Action<bool?> OnDialogClosed { get; set; } = null!;
    public bool DialogResult { get; private set; }
}