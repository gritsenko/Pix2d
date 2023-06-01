using Pix2d.Drawing.Tools;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using Pix2d.ViewModels.ToolBar;
using Pix2d.Views.ToolBar.Tools;

namespace Pix2d.Views.ToolBar;

public class ToolSettingsView : ViewBaseSingletonVm<ToolBarViewModel>
{
    public static void ListItemStyle(ListBoxItem i) => i
        .FontFamily(StaticResources.Fonts.IconFontSegoe)
        .FontSize(28)
        .MinWidth(50)
        .MinHeight(50)
        .HorizontalContentAlignment(HorizontalAlignment.Center)
        .VerticalContentAlignment(VerticalAlignment.Center);

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
        new FuncDataTemplate<BrushTool>((vm, ns) => new BrushToolSettingsView());

    public IDataTemplate FillToolSettingsTemplate { get; } =
        new FuncDataTemplate<FillTool>((vm, ns) =>
            new StackPanel()
                .Margin(8)
                .Children(
                    new TextBlock()
                        .Text("Erase mode"),
                    new ToggleSwitch()
                        .IsChecked(false)
                )
        );

    public IDataTemplate PixelSelectToolSettingsTemplate { get; } =
        new FuncDataTemplate<PixelSelectTool>((vm, ns) =>
            new ListBox()
                .SelectedIndex(0)
                .OnSelectionChanged(args =>
                {
                    //if(args.AddedItems.Count > 0 && args.AddedItems[0] is ListBoxItem item)
                    //    vm.SelectModeCommand.Execute(item.DataContext);
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
}