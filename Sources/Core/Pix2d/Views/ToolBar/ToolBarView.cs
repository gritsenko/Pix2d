using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Pix2d.ViewModels;
using Pix2d.ViewModels.Color;
using Pix2d.ViewModels.ToolBar;

namespace Pix2d.Views.ToolBar;

public class ToolBarView : ViewBaseSingletonVm<ToolBarViewModel>
{
    private BrushToolSettingsViewModel BrushToolSettingsViewModel => GetViewModel<BrushToolSettingsViewModel>();

    public ToolBarView()
    {
        this.Styles.Add(
            new Style<Button>(s => s.Class("wide").Descendant().OfType<Button>().Class("toolbar-button"))
                .Width(52)
                .Height(52)
        );

        this.Styles.Add(
            new Style<Button>(s => s.Class("wide").Descendant().OfType<Button>().Class("color-button"))
                .Width(40)
                .Height(40)
        );

        this.Styles.Add(
            new Style<Button>(s => s.Class("small").Descendant().OfType<Button>().Class("toolbar-button"))
                .Width(40)
                .Height(40)
        );

        this.Styles.Add(
            new Style<Button>(s => s.Class("small").Descendant().OfType<Button>().Class("color-button"))
                .Width(32)
                .Height(32)
        );

        this.Styles.Add(
            new Style<TextBlock>(s => s.Class("small").Descendant().OfType<Button>().Class("color-button").OfType<TextBlock>().Class("ToolIcon"))
                .FontSize(16)
        );

    }

    protected override object Build(ToolBarViewModel vm) =>
        new StackPanel()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(

                new Button() //Color picker button
                    .Classes("color-button")
                    .IsVisible(@vm.IsSpriteEditMode)
                    .Margin(0, 8)
                    .Command(GetViewModel<MainViewModel>().ToggleColorEditorCommand)
                    .CornerRadius(25)
                    .BorderThickness(3)
                    .BorderBrush(Colors.White.ToBrush())
                    .Background(Bind(GetViewModel<ColorPickerViewModel>(), m => m.SelectedColor).Converter(StaticResources.Converters.SKColorToBrushConverter)),

                new Button() //Brush settings button
                    .Classes("toolbar-button")
                    .IsVisible(@vm.IsSpriteEditMode)
                    .Background("#414953".ToColor().ToBrush())
                    .Margin(0, 8)
                    .Padding(0)
                    .Command(GetViewModel<MainViewModel>().ToggleBrushSettingsCommand)
                    .Content(Bind(BrushToolSettingsViewModel, m => m.CurrentPixelBrushSetting))
                    .ContentTemplate(BrushPreviewTemplate),

                new ItemsControl() //tools list
                    .Items(ViewModel.Tools)
                    .ItemTemplate(ToolItemTemplate)
            );

    public static FuncDataTemplate<BrushPresetViewModel> BrushPreviewTemplate { get; set; } = new((itemVm, ns) =>
        new Grid()
            .DataContext(itemVm.Preview)
            .Background(new Binding("Bitmap") { Converter = StaticResources.Converters.SKBitmapToBrushConverter })
            .Width(50)
            .Height(50)
    );

    private IDataTemplate ToolItemTemplate =>
        new FuncDataTemplate<ToolItemViewModel>((itemVm, ns) =>
            new Grid().Children(
                new Border()
                    .BorderThickness(4, 0, 0, 0)
                    .BorderBrush(StaticResources.Brushes.SelectedHighlighterBrush)
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .IsVisible(@itemVm.IsSelected),

                new Button()
                    .Classes("toolbar-button")
                    .Command(ViewModel.SelectToolCommand)
                    .CommandParameter(new Binding())
                    .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                    .Background(Colors.Transparent.ToBrush())
                    .Content(itemVm)
                    .ToolTip(itemVm.Tooltip),

                new Path()
                    .Data(Geometry.Parse("F1 M 4,0L 4,4L 0,4"))
                    .Fill(Color.Parse("#FFCCCCCC").ToBrush())
                    .Width(8)
                    .Height(8)
                    .Stretch(Stretch.Fill)
                    .VerticalAlignment(VerticalAlignment.Bottom)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .IsVisible(new Binding("HasToolProperties"))
            ));

}