using Avalonia.Styling;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;
using SkiaNodes.Interactive;

namespace Pix2d.UI.Dialogs;

public class InputDialogView : ComponentBase, IDialogView
{
    private string _value;
    private string _message;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SKInput.Current.KeyPressed += OnKeyPressed;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        SKInput.Current.KeyPressed -= OnKeyPressed;
    }

    private void OnKeyPressed(object sender, KeyboardActionEventArgs e)
    {
        if (e.Key == VirtualKeys.Escape && e.Modifiers == KeyModifier.None)
        {
            Cancel();
            e.Handled = true;
        } else if (e.Key == VirtualKeys.Return && e.Modifiers == KeyModifier.None)
        {
            Confirm();
            e.Handled = true;
        }
    }

    protected override object Build() =>
        new Grid()
            .Rows("*,auto,48")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .TextWrapping(TextWrapping.Wrap)
                    .Margin(new Thickness(16, 0))
                    .Text(Message, BindingMode.OneWay, bindingSource: this),
                
                new TextBox().Row(1)
                    .Margin(new Thickness(16, 0))
                    .With(Focus)
                    .Text(Value, BindingMode.TwoWay),

                new StackPanel().Row(2)
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Styles(
                        new Style(s => s.OfType(typeof(Button)))
                        {
                            Setters =
                            {
                                new Setter(Button.MarginProperty, new Thickness(8,0)),
                                new Setter(TemplatedControl.BackgroundProperty, StaticResources.Brushes.ButtonSolidBrush),
                                new Setter(Button.WidthProperty, 100d)
                            }
                        }) //styles

                    .Children(
                        new Button()
                            .Content("Ok")
                            .Background(StaticResources.Brushes.AccentBrush)
                            .OnClick(_ =>
                            {
                                Confirm();
                            }),
                        new Button()
                            .Content("Cancel")
                            .OnClick(_ =>
                            {
                                Cancel();
                            })


                    ) //stack panel children
            );

    private void Confirm()
    {
        OnDialogClosed?.Invoke(true);
    }

    private void Cancel()
    {
        Value = null;
        OnDialogClosed?.Invoke(null);
    }

    private void Focus(TextBox obj)
    {
        obj.AttachedToVisualTree += (_, _) =>
        {
            obj.SelectAll();
            obj.Focus();
        };
    }

    public string Title { get; set; }

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
        }
    }

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
}