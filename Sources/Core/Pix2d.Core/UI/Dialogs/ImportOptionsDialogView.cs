#nullable enable
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Import.Flow;
using Pix2d.Abstract.UI;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Dialogs;

/// <summary>
/// Lets the user choose how an ambiguous set of images should be imported (layers / new sprites /
/// animation frames). The mode detected as the default is highlighted. Cancel returns <c>null</c>.
/// </summary>
public partial class ImportOptionsDialogView : ViewBase<ImportOptionsDialogView.State>, IDialogView<ImportMode?>
{
    public ImportOptionsDialogView(string summary, ImportMode defaultMode, bool allowLayers)
        : base(new State { Summary = summary, DefaultMode = defaultMode, AllowLayers = allowLayers })
    {
    }

    protected override object Build(State state) =>
        new Grid()
            .Rows("Auto,*,48")
            .Children(
                new TextBlock()
                    .Row(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(16, 12, 16, 0))
                    .TextWrapping(TextWrapping.Wrap)
                    .Text(state, x => x.Summary),

                new StackPanel()
                    .Row(1)
                    .Orientation(Orientation.Vertical)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(new Thickness(16))
                    .Styles(
                        new Style<Button>
                        {
                            Setters =
                            {
                                new Setter(Button.MarginProperty, new Thickness(0, 4)),
                                new Setter(Button.WidthProperty, 220d),
                                new Setter(Button.HorizontalContentAlignmentProperty, HorizontalAlignment.Center)
                            }
                        })
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Content("Add as layers")
                            .IsVisible(state.AllowLayers)
                            .Background(BrushFor(state, ImportMode.Layers))
                            .OnClick(_ => Choose(ImportMode.Layers)),
                        new Button()
                            .Classes("btn")
                            .Content("New sprite(s)")
                            .Background(BrushFor(state, ImportMode.NewSprites))
                            .OnClick(_ => Choose(ImportMode.NewSprites)),
                        new Button()
                            .Classes("btn")
                            .Content("Animation frames")
                            .Background(BrushFor(state, ImportMode.AnimationFrames))
                            .OnClick(_ => Choose(ImportMode.AnimationFrames))
                    ),

                new Button()
                    .Row(2)
                    .Classes("btn")
                    .Content("Cancel")
                    .Width(100)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .OnClick(_ =>
                    {
                        DialogResult = null;
                        OnDialogClosed?.Invoke(false);
                    })
            );

    private static Brush BrushFor(State state, ImportMode mode) =>
        state.DefaultMode == mode ? StaticResources.Brushes.AccentBrush : StaticResources.Brushes.ButtonSolidBrush;

    private void Choose(ImportMode mode)
    {
        DialogResult = mode;
        OnDialogClosed?.Invoke(true);
    }

    public string Title { get; set; } = "Import";
    public Action<bool?> OnDialogClosed { get; set; } = null!;
    public ImportMode? DialogResult { get; private set; }

    public sealed partial class State : ObservableObject
    {
        [ObservableProperty]
        public partial string Summary { get; set; } = string.Empty;

        public ImportMode DefaultMode { get; set; }

        public bool AllowLayers { get; set; }
    }
}
