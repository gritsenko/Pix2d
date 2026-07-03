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
                .IsVisible(false),

            // Icon-only: hide the caption (its "Auto" row collapses to 0).
            new Style<TextBlock>(s => s.OfType<TextBlock>().Name(AppButton.LabelControlName))
                .IsVisible(false),

            // Square, compact buttons matching the top bar. Outer control + inner ToggleButton.
            new Style<AppButton>(s => s.Is<AppButton>())
                .Width(StaticResources.Measures.CompactAppButtonSize)
                .Height(StaticResources.Measures.CompactAppButtonSize)
                .Margin(StaticResources.Measures.CompactButtonMargin),

            new Style<ToggleButton>(s => s.OfType<ToggleButton>())
                .Width(StaticResources.Measures.CompactAppButtonSize)
                .Height(StaticResources.Measures.CompactAppButtonSize)
                .Margin(0)
                // 12px (matching wide mode) so the checked highlight nests concentrically inside the
                // 16px panel — the flat 6px compact radius read as mismatched against the rounded panel.
                .CornerRadius(12),
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

            _appState.WatchForCurrentProject(x => x.CurrentContextType, SyncFromAppState);
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