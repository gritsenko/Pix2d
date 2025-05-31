#nullable enable
using Avalonia.Styling;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class AlertDialog : ComponentBase, IDialogView<object?>
{
    protected override object Build() =>
        new Grid()
            .Rows("*,48")
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(16, 0))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(Message, BindingMode.OneWay, bindingSource: this),

                new StackPanel().Row(1)
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Styles(
                        new Style<Button>
                        {
                            Setters =
                            {
                                new Setter(Button.MarginProperty, new Thickness(8,0)),
                                new Setter(Button.BackgroundProperty, StaticResources.Brushes.ButtonSolidBrush),
                                new Setter(Button.WidthProperty, 100d)
                            }
                        }) //styles

                    .Children(
                        new Button()
                            .Classes("btn")
                            .Content("OK")
                            .Background(StaticResources.Brushes.AccentBrush)
                            .OnClick(_ => OnDialogClosed?.Invoke(true))

                    ) //stack panel children
            );

    public string Title { get; set; }
    
    private string _message = "Problem!";
    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged();
        }
    }
    public Action<bool?> OnDialogClosed { get; set; }

    public object? DialogResult => null;
}