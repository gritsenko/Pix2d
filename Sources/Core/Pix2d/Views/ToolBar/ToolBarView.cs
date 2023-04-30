using Avalonia.Styling;
using Pix2d.ViewModels;
using Pix2d.ViewModels.Color;
using Pix2d.ViewModels.ToolBar;
using Pix2d.ViewModels.ToolBar.ToolSettings;
using Pix2d.ViewModels.ToolSettings;

namespace Pix2d.Views.ToolBar;

public class ToolBarView : ViewBaseSingletonVm<ToolBarViewModel>
{
    private BrushToolSettingsViewModel BrushToolSettingsViewModel => GetViewModel<BrushToolSettingsViewModel>();

    public ToolBarView()
    {
        Selector WideButtonSelector(Selector s) => s.Class("wide").Descendant().OfType<Button>();
        Selector SmallButtonSelector(Selector s) => s.Class("small").Descendant().OfType<Button>();

        this.Styles.AddRange(new IStyle[]
        {
            new Style<Button>(s => WideButtonSelector(s).Class("toolbar-button")).Width(52).Height(52),
            new Style<Button>(s => WideButtonSelector(s).Class("color-button")).Width(40).Height(40),

            new Style<Button>(s => SmallButtonSelector(s).Class("toolbar-button")).Width(40).Height(40),
            new Style<Button>(s => SmallButtonSelector(s).Class("color-button")).Width(32).Height(32),
            new Style<TextBlock>(
                s => SmallButtonSelector(s).Class("color-button").OfType<TextBlock>().Class("ToolIcon")).FontSize(16)
        });
    }

    protected override object Build(ToolBarViewModel vm) =>
        new StackPanel()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(

                new Button() //Color picker button
                    .Classes("color-button")
                    .IsVisible(@vm.IsSpriteEditMode)
                    .Margin(0, 8)
                    .Command(Commands.View.ToggleColorEditorCommand)
                    .CornerRadius(25)
                    .BorderThickness(3)
                    .BorderBrush(Colors.White.ToBrush())
                    .Background(Bind(GetViewModel<ColorPickerViewModel>(), m => m.SelectedColor)
                        .Converter(StaticResources.Converters.SKColorToBrushConverter)),

                new Button() //Brush settings button
                    .Classes("toolbar-button")
                    .IsVisible(@vm.IsSpriteEditMode)
                    .Background("#414953".ToColor().ToBrush())
                    .Margin(0, 8)
                    .Padding(0)
                    .Command(Commands.View.ToggleBrushSettingsCommand)
                    .Content(Bind(BrushToolSettingsViewModel, m => m.CurrentPixelBrushSetting))
                    .ContentTemplate(new FuncDataTemplate<BrushPresetViewModel>((itemVm, ns) =>
                        new Grid().DataContext(itemVm.Preview)
                            .Background(new Binding("Bitmap") { Converter = StaticResources.Converters.SKBitmapToBrushConverter })
                            .Width(50)
                            .Height(50))),

                new ItemsControl() //tools list
                    .Items(ViewModel.Tools)
                    .ItemTemplate(
                        new FuncDataTemplate<ToolItemViewModel>((itemVm, ns) =>
                            new ToolItemView(itemVm)
                                .SelectToolCommand(ViewModel.SelectToolCommand)))
            );
}