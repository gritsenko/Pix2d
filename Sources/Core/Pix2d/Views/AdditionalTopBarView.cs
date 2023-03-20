using Pix2d.ViewModels;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views;

public partial class AdditionalTopBarView : ViewBaseSingletonVm<MainViewModel>
{
    protected override object Build(MainViewModel vm) =>
        new Grid()
            .Height(32)
            .Cols("32,33,Auto,Auto")
            .Children(

                // background
                new Rectangle().Col(0).ColSpan(5)
                    .Fill(StaticResources.Brushes.MainBackgroundBrush)
                    .Stroke(StaticResources.Brushes.MainBackgroundBrush)
                    .StrokeThickness(2),

                //toggle grid 
                new Button().Col(0)
                    .Foreground(@vm.ShowGrid, StaticResources.Converters.BoolToBrushButtonForegroundConverter)
                    .With(ButtonStyle)
                    .Command(ViewModel.ToggleGridCommand)
                    .Content("\xE80A"),

                //toggle preview window
                new Button().Col(1)
                    .Foreground(@vm.ShowPreviewPanel, StaticResources.Converters.BoolToBrushButtonForegroundConverter)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .With(ButtonStyle)
                    .Command(ViewModel.TogglePreviewPanelCommand)
                    .Content("\xE91B"),

                new ZoomPanelView().Col(2)
                    .Margin(new Thickness(1, 0, 0, 0)),

                //toggle layers
                new ToggleButton().Col(3)
                    .IsVisible(@vm.CanShowLayers)
                    .Width(64)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .BorderBrush(StaticResources.Brushes.SelectedItemBrush)
                    .FontSize(16)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .IsChecked(@vm.ShowLayers, BindingMode.TwoWay)
                    .Content("\xE81E")

            );

    private void ButtonStyle(Button b) => b
        .Background(StaticResources.Brushes.SelectedItemBrush)
        .BorderBrush(StaticResources.Brushes.SelectedItemBrush)
        .FontSize(16)
        .FontFamily(StaticResources.Fonts.IconFontSegoe);

}