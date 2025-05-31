using Avalonia.Styling;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

public class AdditionalTopBarView : LocalizedComponentBase
{
    protected override StyleGroup BuildStyles() =>
    [
        new Style<AdditionalTopBarView>(s => s.OfType<AdditionalTopBarView>())
            .VerticalAlignment(VerticalAlignment.Bottom)
            .HorizontalAlignment(HorizontalAlignment.Right),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<AppToggleButton>(s=>s.OfType<AppToggleButton>().Name("preview-button"))
                .IsVisible(false)
        }
    ];

    protected override object Build() =>
        new BlurPanel()
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        //toggle preview window
                        new AppToggleButton()
                            .Name("preview-button")
                            .IsChecked(() => AppState.UiState.ShowPreviewPanel, v => AppState.UiState.ShowPreviewPanel = v)
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Label(L("Preview"))
                            .Content("\xe903"),

                        new AppToggleButton()
                            .IsChecked(() => AppState.UiState.ShowTimeline, v => AppState.UiState.ShowTimeline = v)
                            .Label(L("Animate"))
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Content("\xe905"),

                        new AppToggleButton()
                            .IsVisible(() => AppState.CurrentProject.CurrentContextType == EditContextType.Sprite)
                            .IsChecked(() => AppState.UiState.ShowLayers, v => AppState.UiState.ShowLayers = v)
                            .Label(L("Layers"))
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Content("\xe900")
                    )
            );
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private ISettingsService SettingsService { get; set; } = null!;

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
                SettingsService.Set(nameof(AppState.UiState.ShowLayers), AppState.UiState.ShowLayers);
            }
        });
    }
}