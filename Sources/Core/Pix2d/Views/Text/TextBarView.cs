using Pix2d.Plugins.Drawing.ViewModels;
using Pix2d.Plugins.Sprite;
using Pix2d.Primitives;

namespace Pix2d.Views.Text;

public class TextBarView : ViewBaseSingletonVm<TextBarViewModel> {
    protected override object Build(TextBarViewModel vm) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(new Control[] {
                new Button() //ENTER TEXT FLYOUT
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout()
                            .Placement(PlacementMode.Bottom);
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
                new Button() //FONT PROPERTIES FLYOUT
                    .With(ButtonStyle)
                    .With(b =>
                    {
                        var flyout = new Flyout()
                            .Placement(PlacementMode.Bottom)
                            .Content(new StackPanel()
                                .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                                .Orientation(Orientation.Horizontal)
                                .Children(new Control[]
                                    {
                                        new TextBlock()
                                            .Margin(8,0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font"),

                                        new ComboBox()
                                            .Width(140)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Items(vm.Fonts)
                                            .SelectedItem(@vm.SelectedFont, BindingMode.TwoWay)
                                            .ItemTemplate(
                                                (FontItemViewModel item) => new TextBlock().Text(item?.Name ?? "")),

                                        new TextBlock()
                                            .Margin(8,0)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Text("Font size"),

                                        new NumericUpDown()
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Value(vm.FontSize, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DD")
                                            .IsChecked(@vm.IsBold, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8DB")
                                            .IsChecked(@vm.IsItalic, BindingMode.TwoWay),

                                        new ToggleButton()
                                            .With(ButtonStyle)
                                            .VerticalAlignment(VerticalAlignment.Center)
                                            .Content("\xE8D2")
                                            .IsChecked(@vm.IsAliased, BindingMode.TwoWay),
                                    }
                                )
                            );
                        b.Click += (s, e) => flyout.ShowAt(b);

                    })
                    .Content("\xE8D2"),

                new Button()
                    .Command(vm.CancelCommand)
                    .With(ButtonStyle)
                    .Content("\xE711"),

                new Button()
                    .Command(vm.ApplyCommand)
                    .With(ButtonStyle)
                    .Content("\xE73E")
            });

    private void ButtonStyle(Button b) {
        b.Classes("AppBarButton")
            .Background(Colors.Transparent.ToBrush())
            .BorderBrush(Colors.Transparent.ToBrush())
            .Width(48)
            .Height(48)
            .FontSize(22)
            .FontFamily(StaticResources.Fonts.IconFontSegoe)
            .Padding(new Thickness(0));

        if (b.Command is Pix2dCommand pc) {
            b.ToolTip(pc.Tooltip);
        }
    }

}