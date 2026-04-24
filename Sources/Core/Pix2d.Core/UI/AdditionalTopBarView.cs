using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

public partial class AdditionalTopBarView(AppState appState, ISettingsService settingsService)
    : ViewBase<AdditionalTopBarView.State>(new State(appState, settingsService))
{
    protected override StyleGroup BuildStyles() =>
    [
        new Style<AdditionalTopBarView>()
            .VerticalAlignment(VerticalAlignment.Bottom)
            .HorizontalAlignment(HorizontalAlignment.Right),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<AppToggleButton>(s=>s.OfType<AppToggleButton>().Name("preview-button"))
                .IsVisible(false)
        }
    ];

    protected override object Build(State state) =>
        new BlurPanel()
            .Content(
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .Children(

                        //toggle preview window
                        new AppToggleButton()
                            .Name("preview-button")
                            .IsChecked(state, x => x.ShowPreviewPanel, BindingMode.TwoWay)
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Label(L("Preview"))
                            .Content("\xe903"),

                        new AppToggleButton()
                            .IsChecked(state, x => x.ShowTimeline, BindingMode.TwoWay)
                            .Label(L("Animate"))
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Content("\xe905"),

                        new AppToggleButton()
                            .IsVisible(state, x => x.IsSpriteContext)
                            .IsChecked(state, x => x.ShowLayers, BindingMode.TwoWay)
                            .Label(L("Layers"))
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Content("\xe900")
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ISettingsService _settingsService;
        private bool _isSyncing;

        [ObservableProperty]
        public partial bool ShowPreviewPanel { get; set; }

        [ObservableProperty]
        public partial bool ShowTimeline { get; set; }

        [ObservableProperty]
        public partial bool ShowLayers { get; set; }

        [ObservableProperty]
        public partial bool IsSpriteContext { get; set; }

        public State(AppState appState, ISettingsService settingsService)
        {
            _appState = appState;
            _settingsService = settingsService;

            SyncFromAppState();

            _appState.CurrentProject.WatchFor(x => x.CurrentContextType, SyncFromAppState);
            _appState.UiState.WatchFor(x => x.ShowPreviewPanel, SyncFromAppState);
            _appState.UiState.WatchFor(x => x.ShowTimeline, SyncFromAppState);
            _appState.UiState.WatchFor(x => x.ShowLayers, SyncFromAppState);
        }

        partial void OnShowPreviewPanelChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.UiState.ShowPreviewPanel = value;
        }

        partial void OnShowTimelineChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.UiState.ShowTimeline = value;
        }

        partial void OnShowLayersChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.UiState.ShowLayers = value;
            _settingsService.Set(nameof(AppState.UiState.ShowLayers), value);
        }

        private void SyncFromAppState()
        {
            _isSyncing = true;
            ShowPreviewPanel = _appState.UiState.ShowPreviewPanel;
            ShowTimeline = _appState.UiState.ShowTimeline;
            ShowLayers = _appState.UiState.ShowLayers;
            IsSpriteContext = _appState.CurrentProject.CurrentContextType == EditContextType.Sprite;
            _isSyncing = false;
        }
    }
}