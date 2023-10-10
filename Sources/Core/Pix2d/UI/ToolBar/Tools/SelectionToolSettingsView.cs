using System.Windows.Input;
using Mvvm;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using Pix2d.UI.Resources;

namespace Pix2d.UI.ToolBar.Tools;

public class SelectionToolSettingsView : ComponentBase
{
    public static void ListItemStyle(ListBoxItem i) => i
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .FontSize(28)
        .MinWidth(50)
        .MinHeight(50)
        .HorizontalContentAlignment(HorizontalAlignment.Center)
        .VerticalContentAlignment(VerticalAlignment.Center);

    protected override object Build() =>
        new ListBox()
            .SelectedIndex(0)
            .OnSelectionChanged(args =>
            {
                if(args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                    SelectModeCommand.Execute(item.DataContext);
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
            );

    public PixelSelectionMode SelectionMode { get; set; }

    public int SelectedIndex { get; set; }

    public ICommand SelectModeCommand => new RelayCommand<PixelSelectionMode>(mode =>
    {
        SelectionMode = mode;
        var dc = this.DataContext as PixelSelectTool;
        if (dc != null)
            dc.SelectionMode = SelectionMode;
    });
}