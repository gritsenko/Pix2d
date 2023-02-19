using Mvvm;
using Pix2d.Plugins.Drawing.ViewModels;
using Pix2d.Plugins.Sprite;
using Pix2d.Primitives;
using Pix2d.Resources;

namespace Pix2d.Views.Text;

public class TextBarView : ViewBaseSingletonVm<TextBarViewModel>
{
    private void ButtonStyle(Button b)
    {
        b.Classes("AppBarButton")
        .Background(Colors.Transparent.ToBrush())
        .BorderBrush(Colors.Transparent.ToBrush())
        .Width(48)
        .Height(48)
        .FontSize(22)
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc)
        {
            b.ToolTip(pc.Tooltip);
        }
    }

    protected override object Build(TextBarViewModel vm) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(new Control[] {
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout() { Placement = FlyoutPlacementMode.Bottom };
                        b.Click += (s, e) => flyout.ShowAt(b);

                        flyout.Content = new Grid()
                            .Children(
                            new TextBox()
                                .Watermark("Enter text")
                                .Text(@vm.Text, BindingMode.TwoWay)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .AcceptsReturn(true)
                                .MinWidth(150)
                            );
                    })
                    .Content("\xF741"),
                new Button()
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new MenuFlyout() { Placement = FlyoutPlacementMode.Bottom };
                        flyout.AddItem("Fill selection", SpritePlugin.EditCommands.FillSelectionCommand);
                        flyout.AddItem("Select object", SpritePlugin.EditCommands.SelectObjectCommand);
                        b.Click += (s, e) => flyout.ShowAt(b);
                    })
                    .Content("\xE8D2"),

                new Button()
                    .Command(SpritePlugin.EditCommands.CropPixels)
                    .With(ButtonStyle)
                    .Content("\xE711"),

                new Button()
                    .Command(SpritePlugin.EditCommands.CropPixels)
                    .With(ButtonStyle)
                    .Content("\xE73E")
            });
}