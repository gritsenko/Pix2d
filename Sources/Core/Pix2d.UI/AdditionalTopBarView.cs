using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Pix2d.Messages;
using Pix2d.UI;

namespace Pix2d.Views;

public class AdditionalTopBarView : ComponentBase
{
    protected override object Build() =>
        new Grid()
            .Styles(
                new Style<Button>()
                    .FontFamily(StaticResources.Fonts.IconFontSegoe),
                new Style<ToggleButton>()
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
                new ToggleButton().Col(0)
                    .Classes("secondary-button")
                    .IsChecked(AppState.CurrentProject.ViewPortState.ShowGrid, BindingMode.TwoWay,
                        bindingSource: AppState.CurrentProject.ViewPortState)
                    .Content("\xE80A"),

                //toggle preview window
                new ToggleButton().Col(1)
                    .Classes("secondary-button")
                    .IsChecked(AppState.UiState.ShowPreviewPanel, BindingMode.TwoWay, bindingSource: AppState.UiState)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .Content("\xE91B"),

                new ZoomPanelView().Col(2)
                    .Margin(new Thickness(1, 0, 0, 0)),

                //toggle layers
                new ToggleButton().Col(3)
                    .Classes("secondary-button")
                    .IsVisible(() => AppState.CurrentProject.CurrentContextType == EditContextType.Sprite)
                    .Width(64)
                    .Margin(new Thickness(1, 0, 0, 0))
                    .FontSize(16)
                    .Styles(LayersButtonStyle)
                    .IsChecked(AppState.UiState.ShowLayers, BindingMode.TwoWay, bindingSource: AppState.UiState)
                    .Content("\xE81E")
            );

    protected Style LayersButtonStyle => new(s => s.OfType<ToggleButton>().Class(":checked").Template().Is<ContentPresenter>())
    {
        Setters =
        {
            new Setter(ToggleButton.BackgroundProperty, StaticResources.Brushes.ButtonActiveBrush),
        }
    };

    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;

    protected override void OnAfterInitialized()
    {
        Messenger.Register<StateChangedMessage>(this, msg =>
        {
            if (msg.PropertyName
               is nameof(AppState.CurrentProject.ViewPortState.ShowGrid)
               or nameof(AppState.UiState.ShowPreviewPanel)
               or nameof(AppState.UiState.ShowLayers)
               or nameof(AppState.CurrentProject.CurrentContextType)
               )
                StateHasChanged();

            if (msg.PropertyName is nameof(AppState.UiState.ShowLayers))
            {
                CoreServices.SettingsService.Set(nameof(AppState.UiState.ShowLayers), AppState.UiState.ShowLayers);
            }
        });
    }
}