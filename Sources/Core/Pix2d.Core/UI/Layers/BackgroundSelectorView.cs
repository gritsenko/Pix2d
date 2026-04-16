using Pix2d.Common.Extensions;
using Pix2d.CommonNodes;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.Layers;

public partial class BackgroundSelectorView(AppState appState, IViewPortRefreshService viewPortRefreshService)
    : ViewBase<BackgroundSelectorView.State>(new State(appState, viewPortRefreshService))
{
    protected override object Build(State state) =>
        new Grid()
            .Children(
                new Button()
                    .Classes("color-button")
                    .Width(32)
                    .Height(32)
                    .CornerRadius(32)
                    .BorderThickness(1)
                    .BorderBrush(Colors.White.WithAlpha(0.3f).ToBrush().ToImmutable())
                    .Background(state, x => x.BackgroundBrush)
                    .Flyout(
                        new Flyout()
                            .Content(
                                new Grid()
                                    .Rows("Auto, Auto, Auto")
                                    .Children(
                                        new TextBlock().Text("Background"),
                                        ViewFactory.Create<Pix2dColorPicker>().Row(1)
                                            .Margin(10)
                                            .Color(state, x => x.BackgroundColor, BindingMode.TwoWay)
                                            .Margin(0, 8)
                                            .Width(200)
                                            .Height(140),
                                        new ToggleSwitch().Row(2)
                                            .IsChecked(state, x => x.ShowBackground, BindingMode.TwoWay)
                                            .Content(L("Show background"))
                                    )
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IViewPortRefreshService _viewPortRefreshService;
        private bool _isSyncing;

        [ObservableProperty]
        public partial SKColor BackgroundColor { get; set; }

        [ObservableProperty]
        public partial bool ShowBackground { get; set; }

        [ObservableProperty]
        public partial IBrush BackgroundBrush { get; set; } = StaticResources.Brushes.CheckerTilesBrush;

        public State(AppState appState, IViewPortRefreshService viewPortRefreshService)
        {
            _appState = appState;
            _viewPortRefreshService = viewPortRefreshService;

            SyncFromSprite();
            _appState.CurrentProject.WatchFor(x => x.CurrentEditedNode, SyncFromSprite);
        }

        partial void OnBackgroundColorChanged(SKColor value)
        {
            UpdateBackgroundBrush();

            if (_isSyncing)
                return;

            if (_appState.SpriteEditorState.BackgroundColor != value)
            {
                _appState.SpriteEditorState.ShowBackground = true;
            }

            _appState.SpriteEditorState.BackgroundColor = value;
            UpdateSprite();
        }

        partial void OnShowBackgroundChanged(bool value)
        {
            UpdateBackgroundBrush();

            if (_isSyncing)
                return;

            _appState.SpriteEditorState.ShowBackground = value;
            UpdateSprite();
        }

        private void SyncFromSprite()
        {
            if (_appState.CurrentProject.CurrentEditedNode is not Pix2dSprite sprite)
                return;

            _isSyncing = true;
            _appState.SpriteEditorState.BackgroundColor = sprite.BackgroundColor;
            _appState.SpriteEditorState.ShowBackground = sprite.UseBackgroundColor;
            BackgroundColor = sprite.BackgroundColor;
            ShowBackground = sprite.UseBackgroundColor;
            _isSyncing = false;

            UpdateBackgroundBrush();
        }

        private void UpdateSprite()
        {
            if (_appState.CurrentProject.CurrentEditedNode is not Pix2dSprite sprite)
                return;

            sprite.BackgroundColor = BackgroundColor;
            sprite.UseBackgroundColor = ShowBackground;
            _viewPortRefreshService.Refresh();
        }

        private void UpdateBackgroundBrush()
        {
            BackgroundBrush = ShowBackground ? BackgroundColor.ToBrush() : StaticResources.Brushes.CheckerTilesBrush;
        }
    }
}