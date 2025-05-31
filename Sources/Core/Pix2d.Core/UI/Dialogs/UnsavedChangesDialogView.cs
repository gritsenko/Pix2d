using Avalonia.Styling;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class UnsavedChangesDialogView : ViewBase, IDialogView<UnsavedChangesDialogResult>
{
    protected override object Build() =>
        new Grid()
            .Rows("*,48")
            .Children(
                new TextBlock()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Text("You have unsaved changes"),

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
                                new Setter(Button.BackgroundProperty, StaticResources.Brushes.ButtonSolidBrush),
                                new Setter(Button.WidthProperty, 100d)
                            }
                        }) //styles

                    .Children(
                        new Button()
                            .Classes("btn")
                            .Content("Save")
                            .Background(StaticResources.Brushes.AccentBrush)
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.Yes;
                                OnDialogClosed?.Invoke(true);
                            }),
                        new Button()
                            .Classes("btn")
                            .Content("Discard")
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.No;
                                OnDialogClosed?.Invoke(false);
                            }),
                        new Button()
                            .Classes("btn")
                            .Content("Cancel")
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.Cancel;
                                OnDialogClosed?.Invoke(null);
                            })


                    ) //stack panel children
            );

    public string Title { get; set; }
    public Action<bool?> OnDialogClosed { get; set; }
    public UnsavedChangesDialogResult DialogResult { get; set; }
}