using Pix2d.Mvvm;
using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public partial class ClipboardActionsView : ComponentBase
{

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new Button()
                    .With(ButtonStyle)
                    .Command(MainViewModel?.PasteCommand)
                    .Content("\xE77F"),
                new Button()
                    .With(ButtonStyle)
                    .Command(MainViewModel?.CopyCommand)
                    .Content("\xE8C8"),
                new Button()
                    .With(ButtonStyle)
                    .Command(MainViewModel?.CutCommand)
                    .Content("\xE8C6"),
                new Button()
                    .With(ButtonStyle)
                    .Command(MainViewModel?.CropCommand)
                    .Content("\xE7A8"),
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new MenuFlyout() { Placement = FlyoutPlacementMode.Bottom };
                        flyout.AddItem("Fill selection", MainViewModel?.FillSelectionCommand);
                        flyout.AddItem("Select object", MainViewModel?.SelectObjectCommand);
                        b.Click += (s, e) => flyout.ShowAt(b);
                    })
                    .Content("\xE10C")
            );

    [Inject] IViewModelService ViewModelService { get; set; }
    public MainViewModel MainViewModel => ViewModelService?.GetViewModel<MainViewModel>();

    private void ButtonStyle(Button b) => b
        .Classes("AppBarButton")
        .Background(Colors.Transparent.ToBrush())
        .BorderBrush(Colors.Transparent.ToBrush())
        .Width(48)
        .Height(48)
        .FontSize(16)
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .Padding(new Thickness(0));
}