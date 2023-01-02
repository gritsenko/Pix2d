using Pix2d.Resources;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public partial class ClipboardActionsView : ViewBaseSingletonVm<MainViewModel>
{

    protected override object Build(MainViewModel vm) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new Button()
                    .With(ButtonStyle)
                    .Command(vm?.PasteCommand)
                    .Content("\xE77F"),
                new Button()
                    .With(ButtonStyle)
                    .Command(vm?.CopyCommand)
                    .Content("\xE8C8"),
                new Button()
                    .With(ButtonStyle)
                    .Command(vm?.CutCommand)
                    .Content("\xE8C6"),
                new Button()
                    .With(ButtonStyle)
                    .Command(vm?.CropCommand)
                    .Content("\xE7A8"),
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new MenuFlyout() { Placement = FlyoutPlacementMode.Bottom };
                        flyout.AddItem("Fill selection", vm?.FillSelectionCommand);
                        flyout.AddItem("Select object", vm?.SelectObjectCommand);
                        b.Click += (s, e) => flyout.ShowAt(b);
                    })
                    .Content("\xE10C")
            );

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