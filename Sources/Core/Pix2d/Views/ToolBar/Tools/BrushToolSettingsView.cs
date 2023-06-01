using Pix2d.Primitives.Drawing;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views.ToolBar.Tools;

public class BrushToolSettingsView : ComponentBase
{
    public static void ListItemStyle(ListBoxItem i) => i
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .FontSize(28)
        .MinWidth(50)
        .MinHeight(50)
        .HorizontalContentAlignment(HorizontalAlignment.Center)
        .VerticalContentAlignment(VerticalAlignment.Center);

    protected override object Build()
        => new Border()
            .Child(

                new ListBox()
                    .VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                    .SelectedIndex(0)
                    .OnSelectionChanged(args =>
                    {
                        //if (args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                        //    vm.SelectShapeCommand.Execute(item.DataContext);
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
                    ) //List box Items
            ); //Border child

}