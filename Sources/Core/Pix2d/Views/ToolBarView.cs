using Pix2d.Resources;
using Pix2d.ViewModels;
using Pix2d.ViewModels.Color;
using Pix2d.ViewModels.ToolBar;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views
{
    public class ToolBarView : ViewBaseSingletonVm<ToolBarViewModel>
    {
        private BrushToolSettingsViewModel BrushToolSettingsViewModel => GetViewModel<BrushToolSettingsViewModel>();
        protected override object Build(ToolBarViewModel vm) =>
            new StackPanel()
                .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                .Children(

                    new Button() //COlor picker button
                        .IsVisible(@vm.IsSpriteEditMode)
                        .Margin(0, 8)
                        .Height(40)
                        .Width(40)
                        .Command(GetViewModel<MainViewModel>().ToggleColorEditorCommand)
                        .CornerRadius(25)
                        .BorderThickness(3)
                        .BorderBrush(Colors.White.ToBrush())
                        .Background(Bind(GetViewModel<ColorPickerViewModel>(), m => m.SelectedColor).Converter(StaticResources.Converters.SKColorToBrushConverter)),

                    new Button() //Brush settings button
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
                        .Command(ViewModel.SelectToolCommand)
                        .CommandParameter(new Binding())
                        .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                        .Background(Colors.Transparent.ToBrush())
                        .Width(52)
                        .Height(52)
                        .With(_ => { })
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
}