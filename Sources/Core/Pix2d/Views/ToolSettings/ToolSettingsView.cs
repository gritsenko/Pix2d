using Pix2d.Primitives.Drawing;
using Pix2d.ViewModels.ToolBar;
using Pix2d.ViewModels.ToolSettings;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views.ToolSettings;

public class ToolSettingsView : ViewBaseSingletonVm<ToolBarViewModel>
{
    protected override object Build(ToolBarViewModel vm) =>
        new ContentControl()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .DataTemplates(
                BrushToolSettingsTemplate,
                FillToolSettingsTemplate,
                PixelSelectToolSettingsTemplate
            )
            .Content(
                @vm.SelectedToolSettings
            );

    public IDataTemplate BrushToolSettingsTemplate { get; } =
        new FuncDataTemplate<BrushToolSettingsViewModel>((vm, ns) =>
                new Border()
                    .Child(

                        new ListBox()
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                            .SelectedIndex(@vm.SelectedIndex)
                            .OnSelectionChanged(args =>
                            {
                                if (args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                                    vm.SelectShapeCommand.Execute(item.DataContext);
                            })
                            .Items(
                                new ListBoxItem()
                                    .With(ListItemStyle)
                                    .DataContext(ShapeType.Free)
                                    .Content("\xE70F")
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .FontSize(26),

                                new ListBoxItem()
                                    .With(ListItemStyle)
                                    .DataContext(ShapeType.Rectangle)
                                    .Content("\xe920")
                                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                    .FontSize(28),

                                new ListBoxItem()
                                    .With(ListItemStyle)
                                    .DataContext(ShapeType.Line)
                                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                    .FontSize(28)
                                    .Content(
                                        new Path()
                                            .Data(Geometry.Parse(
                                                "M 26.28125 4.28125 L 4.28125 26.28125 L 5.71875 27.71875 L 27.71875 5.71875 Z "))
                                            .Fill(Brushes.White)
                                            .Width(28)
                                            .Height(28)
                                            .Stretch(Stretch.Uniform)

                                    ),
                                new ListBoxItem()
                                    .With(ListItemStyle)
                                    .DataContext(ShapeType.Oval)
                                    .Content("\xe908")
                                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                    .FontSize(28),

                                new ListBoxItem()
                                    .With(ListItemStyle)
                                    .DataContext(ShapeType.Triangle)
                                    .Content("\xe927")
                                    .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
                                    .FontSize(28)
                            )//List box Items
                    )//Border child
        );

    public IDataTemplate FillToolSettingsTemplate { get; } =
        new FuncDataTemplate<FillToolSettingsViewModel>((vm, ns) =>
            new StackPanel()
                .Margin(8)
                .Children(
                    new TextBlock()
                        .Text("Erase mode"),
                    new ToggleSwitch()
                        .IsChecked(@vm.EraseMode)
                )
        );

    public IDataTemplate PixelSelectToolSettingsTemplate { get; } =
        new FuncDataTemplate<SelectionToolSettingsViewModel>((vm, ns) =>
            new ListBox()
                .SelectedIndex(@vm.SelectedIndex)
                .OnSelectionChanged(args =>
                {
                    if(args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                        vm.SelectModeCommand.Execute(item.DataContext);
                })
                .Items(
                    new ListBoxItem()
                        .With(ListItemStyle)
                        .Content("\xF407")
                        .DataContext(PixelSelectionMode.Rectangle),

                    new ListBoxItem()
                        .Content("\xf408")
                        .With(ListItemStyle)
                        .DataContext(PixelSelectionMode.Freeform),

                    new ListBoxItem()
                        .Content("\xe790")
                        .With(ListItemStyle)
                        .DataContext(PixelSelectionMode.SameColor)
                )
        );


    public static void ListItemStyle(ListBoxItem i) => i
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .FontSize(28)
        .MinWidth(50)
        .MinHeight(50)
        .HorizontalContentAlignment(HorizontalAlignment.Center)
        .VerticalContentAlignment(VerticalAlignment.Center);

}