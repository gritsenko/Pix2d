using Avalonia.Styling;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class YesNoDialogView : ViewBase, IDialogView
{
    public static readonly DirectProperty<YesNoDialogView, string> MessageProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(Message), o => o.Message, (o, v) => o.Message = v);

    private string _message;

    public string Message
    {
        get => _message;
        set => SetAndRaise(MessageProperty, ref _message, value);
    }

    public static readonly DirectProperty<YesNoDialogView, string> OkLabelProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(OkLabel), o => o.OkLabel, (o, v) => o.OkLabel = v);

    private string _okLabel;

    public string OkLabel
    {
        get => _okLabel;
        set => SetAndRaise(OkLabelProperty, ref _okLabel, value);
    }
    
    public static readonly DirectProperty<YesNoDialogView, string> CancelLabelProperty
        = AvaloniaProperty.RegisterDirect<YesNoDialogView, string>(nameof(CancelLabel), o => o.CancelLabel, (o, v) => o.CancelLabel = v);

    private string _cancelLabel;

    public string CancelLabel
    {
        get => _cancelLabel;
        set => SetAndRaise(CancelLabelProperty, ref _cancelLabel, value);
    }
    
    protected override object Build() =>
        new Grid()
            .Rows("*,48")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(8, 0))
                    .Text(Message, BindingMode.OneWay, bindingSource: this),

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
                            .Content(OkLabel, BindingMode.OneWay, bindingSource: this)
                            .Background(StaticResources.Brushes.AccentBrush)
                            .OnClick(_ =>
                            {
                                DialogResult = true;
                                OnDialogClosed?.Invoke(true);
                            }),
                        new Button()
                            .Content(CancelLabel, BindingMode.OneWay, bindingSource: this)
                            .OnClick(_ =>
                            {
                                DialogResult = false;
                                OnDialogClosed?.Invoke(false);
                            })
                    ) //stack panel children
            );

    public string Title { get; set; }
    public Action<bool?> OnDialogClosed { get; set; }
    public bool DialogResult { get; private set; }
}