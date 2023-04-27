using Avalonia.Controls.Shapes;
using Mvvm.Messaging;
using Pix2d.Messages;

namespace Pix2d.Views;

public class AdditionalTopBarView : ComponentBase
{
    protected override object Build() =>
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
                    .Foreground(Bind(ShowGrid).Converter(StaticResources.Converters.BoolToBrushButtonForegroundConverter))
                    .Command(Commands.View.Snapping.ToggleGrid)
                    .Content("\xE80A"),

                //toggle preview window
                new Button().Col(1)
                    .Foreground(Bind(ShowPreviewPanel).Converter(StaticResources.Converters.BoolToBrushButtonForegroundConverter))
                    .Margin(new Thickness(1, 0, 0, 0))
                    .Command(Commands.View.TogglePreviewPanelCommand)
                    .Content("\xE91B"),

                new ZoomPanelView().Col(2)
                    .Margin(new Thickness(1, 0, 0, 0)),

                //toggle layers
                new Button().Col(3)
                    .IsVisible(Bind(CanShowLayers))
                    .Width(64)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .BorderBrush(StaticResources.Brushes.SelectedItemBrush)
                    .FontSize(16)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .Command(Commands.View.ToggleShowLayersCommand)
                    .Foreground(Bind(ShowLayers).Converter(StaticResources.Converters.BoolToBrushButtonForegroundConverter))
                    .Content("\xE81E")
            );

    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;

    private UiState UiState => AppState.UiState;
    public bool ShowGrid => AppState.CurrentProject.ViewPortState.ShowGrid;
    public bool ShowPreviewPanel => UiState.ShowPreviewPanel;
    public bool ShowLayers => UiState.ShowLayers;
    public bool CanShowLayers => AppState.CurrentProject.CurrentContextType == EditContextType.Sprite;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<StateChangedMessage>(this, msg => StateHasChanged());
    }
}