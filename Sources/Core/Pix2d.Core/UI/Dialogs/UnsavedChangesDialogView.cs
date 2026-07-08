using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

public class UnsavedChangesDialogView : ViewBase, IDialogView<UnsavedChangesDialogResult>
{
    protected override object Build() =>
        new Grid()
            // Cap + margin so it reads as a card on desktop but shrinks to a narrow phone-portrait
            // window (the host PopupView clamps it to the viewport as well).
            .MaxWidth(420)
            .Margin(new Thickness(16))
            .Rows("Auto,Auto")
            .Children(
                new TextBlock()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 0, 0, 16))
                    .TextWrapping(TextWrapping.Wrap)
                    .TextAlignment(TextAlignment.Center)
                    .Text(L("You have unsaved changes")),

                // Three stretched columns instead of a fixed 100px-per-button row, so the buttons
                // always share the width and never overflow a narrow screen.
                new Grid().Row(1)
                    .Cols("*,*,*")
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Margin(new Thickness(0, 0, 4, 0))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Content(L("Save"))
                            .Background(StaticResources.Brushes.AccentBrush)
                            // Accent fill needs crisp, fully-opaque white text — the theme default reads as dull grey.
                            .Foreground(Avalonia.Media.Brushes.White)
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.Yes;
                                OnDialogClosed?.Invoke(true);
                            }),
                        new Button().Col(1)
                            .Classes("btn")
                            .Margin(new Thickness(4, 0))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Background(StaticResources.Brushes.ButtonSolidBrush)
                            .Content(L("Discard"))
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.No;
                                OnDialogClosed?.Invoke(false);
                            }),
                        new Button().Col(2)
                            .Classes("btn")
                            .Margin(new Thickness(4, 0, 0, 0))
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .Background(StaticResources.Brushes.ButtonSolidBrush)
                            .Content(L("Cancel"))
                            .OnClick(_ =>
                            {
                                DialogResult = UnsavedChangesDialogResult.Cancel;
                                OnDialogClosed?.Invoke(null);
                            }))
            );

    public string Title { get; set; } = null!;
    public Action<bool?> OnDialogClosed { get; set; } = null!;
    public UnsavedChangesDialogResult DialogResult { get; set; }
}