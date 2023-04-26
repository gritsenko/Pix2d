using Pix2d.ViewModels;
using Avalonia.Controls.Shapes;
using Mvvm;

namespace Pix2d.Views;

public class AdditionalTopBarView : ViewBaseSingletonVm<AdditionalTopBarView.Model>
{
    protected override object Build(Model vm) =>
        new Grid()
            .Styles(
                new Style<Button>()
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
            )
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
                    .Command(Commands.View.Snapping.ToggleGrid)
                    .Content("\xE80A"),

                //toggle preview window
                new Button().Col(1)
                    .Foreground(@vm.ShowPreviewPanel, StaticResources.Converters.BoolToBrushButtonForegroundConverter)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .Command(Commands.View.TogglePreviewPanelCommand)
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

    public class Model : ObservableObject
    {
        private readonly AppState _state;

        public bool ShowGrid
        {
            get => _state.CurrentProject.ViewPortState.ShowGrid;
            set => _state.CurrentProject.ViewPortState.ShowGrid = value;
        }

        public bool ShowPreviewPanel { get; set; }
        public bool CanShowLayers { get; set; }
        public bool ShowLayers { get; set; }

        public Model(AppState state)
        {
            _state = state;
        }
    }
}