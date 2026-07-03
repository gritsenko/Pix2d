#nullable enable
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public partial class AlertDialog() : ViewBase<AlertDialog.State>(new State()), IDialogView<object?>
{
    protected override object Build(State state) =>
        new Grid()
            .Rows("*,48")
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(16, 0))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(state, x => x.Message),

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
                            // Accent fill needs crisp, fully-opaque white text — the theme default reads as dull grey.
                            .Foreground(Avalonia.Media.Brushes.White)
                            .OnClick(_ => OnDialogClosed?.Invoke(true))

                    ) //stack panel children
            );

    public string Title { get; set; } = string.Empty;

    public string Message
    {
        get => ViewModel?.Message ?? string.Empty;
        set => ViewModel!.Message = value;
    }

    public Action<bool?> OnDialogClosed { get; set; } = null!;

    public object? DialogResult => null;

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial string Message { get; set; } = "Problem!";
    }
}