using Pix2d.Resources;
using Pix2d.Shared;
using Pix2d.ViewModels;

namespace Pix2d.Views;

public class TopBarView : ViewBaseSingletonVm<TopBarViewModel>
{
    protected override object Build(TopBarViewModel vm) =>
        new Grid()
            .Cols("52,*,Auto")
            .Background("#444E59".ToColor().ToBrush())
            .Children(
                new StackPanel().Col(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Orientation(Orientation.Horizontal)
                    .Children(
                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Menu")
                            .Content("\xF12B")
                            .Width(51)
                            .Command(vm.ToggleMenuCommand),
                        new PromoBlockView()
                    ),

                new StackPanel().Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        new AppButton()
                            .With(ButtonStyle)
                            .Command(vm?.ClearLayerCommand)
                            .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Label("Clear")
                            .Content("\xE894"),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Tools")
                            .Command(vm?.ToggleExtraToolsCommand)
                            .Content("\xEC7A"),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Animate")
                            .Command(vm?.ToggleTimelineCommand)
                            .Content("\xED5A"),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Export")
                            .Command(vm?.ShowExportDialogCommand)
                            .Content("\xE72D")
                    ),

                new StackPanel().Col(2)
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        new AppButton().Col(1)
                            .With(ButtonStyle)
                            .Command(vm?.UndoCommand)
                            .Label("Undo")
                            .Content(

                                new Grid()
                                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                                    .VerticalAlignment(VerticalAlignment.Stretch)
                                    .Width(50)
                                    .Height(30)
                                    .Children(

                                    new TextBlock()
                                        .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                        .VerticalAlignment(VerticalAlignment.Center)
                                        .HorizontalAlignment(HorizontalAlignment.Center)
                                        .Text("\xE7A7"),

                                    new TextBlock()
                                        .Margin(new Thickness(0, 0, 4, 4))
                                        .HorizontalAlignment(HorizontalAlignment.Right)
                                        .VerticalAlignment(VerticalAlignment.Top)
                                        .FontSize(12)
                                        .Foreground(Colors.Gray.ToBrush())
                                        .Text(Bind(vm?.UndoSteps))
                                )),

                        new AppButton().Col(1)
                            .With(ButtonStyle)
                            .Label("Redo")
                            .Command(vm?.RedoCommand)
                            .Content("\xE7A6")
                    )
            );

    private void ButtonStyle(AppButton b) => b
        .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
        .Background(Brushes.Green)
        .Width(68)
        .Classes("TopBar");
}