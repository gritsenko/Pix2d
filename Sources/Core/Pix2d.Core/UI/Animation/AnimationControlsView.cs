using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Animation;

public partial class AnimationControlsView : ViewBase<AnimationControlsView.State>
{
    public AnimationControlsView(ICommandService commandService, AppState appState, IMessenger messenger)
        : base(new State(commandService, appState, messenger))
    {
    }

    protected override object Build(State state) =>
        new Grid()
            .Cols("auto,*")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children([

                new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new Button()
                            .Classes("anim-btn")
                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Command(state.SpriteAnimationCommands.Stop)
                            .Content("\xe92e")
                            .Classes("anim-btn"),
                        new Button()
                            .Classes("anim-btn")
                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Command(state.SpriteAnimationCommands.PrevFrame)
                            .Content("\xe92f")
                            .Classes("anim-btn"),
                        new ToggleButton()
                            .Classes("anim-btn")
                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .FontSize(14)
                            .IsChecked(state, x => x.IsPlayingAnimation, BindingMode.OneWay, StaticResources.Converters.InverseBooleanConverter)
                            .Content(state, x => x.PlayIcon)
                            .OnClick(_ => state.SpriteAnimationCommands.TogglePlay.Execute())
                            .Classes("anim-btn"),
                        new Button()
                            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Command(state.SpriteAnimationCommands.NextFrame)
                            .Content("\xe931")
                            .Classes("anim-btn")
                    }
                },

                new ScrollViewer()
                    .Col(1)
                    .HorizontalAlignment(HorizontalAlignment.Right)
                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children([
                                new TextBlock()
                                    .Text(state, x => x.FrameCounterText)
                                    .VerticalAlignment(VerticalAlignment.Center),

                                new Button()
                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Command(state.SpriteAnimationCommands.AddFrame)
                                    .Content("\xe920")
                                    .ToolTip_Tip("Add frame")
                                    .Classes("anim-btn"),
                                new Button()
                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Command(state.SpriteAnimationCommands.DuplicateFrame)
                                    .Content("\xe928")
                                    .ToolTip_Tip("Duplicate frame")
                                    .Classes("anim-btn"),
                                new Button()
                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Command(state.SpriteAnimationCommands.DeleteFrame)
                                    .Content("\xe929")
                                    .ToolTip_Tip("Delete frame")
                                    .Classes("anim-btn"),

                                new ToggleButton()
                                    .Classes("anim-btn")
                                    .VerticalContentAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .IsChecked(state, x => x.ShowOnionSkin, BindingMode.TwoWay, StaticResources.Converters.InverseBooleanConverter)
                                    .ToolTip_Tip("Onion skin")
                                    .Content(new TextBlock()
                                        .FontSize(14)
                                        .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                        .Text("\xe92b")
                                        .Padding(4)),

                                new TextBlock()
                                    .Margin(8, 0, 0, 0)
                                    .Text("Fps")
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new ComboBox()
                                    .ItemsSource(state.FrameRates)
                                    .Margin(8, 0)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .SelectedItem(state, x => x.FrameRate, BindingMode.TwoWay)
                            ]))
            ]);

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private bool _isSyncing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FrameCounterText))]
        public partial int CurrentFrameIndex { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FrameCounterText))]
        public partial int FramesCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayIcon))]
        public partial bool? IsPlayingAnimation { get; set; }

        [ObservableProperty]
        public partial bool? ShowOnionSkin { get; set; }

        [ObservableProperty]
        public partial int FrameRate { get; set; }

        public object PlayIcon => IsPlayingAnimation ?? false ? "\xe92c" : "\xe92d";

        public State(ICommandService commandService, AppState appState, IMessenger messenger)
        {
            _appState = appState;

            SpriteAnimationCommands = commandService.GetCommandList<ISpriteAnimationCommands>()!;
            FrameRates = _appState.SpriteEditorState.FrameRates;

            SyncFromSpriteEditorState();

            _appState.SpriteEditorState.WatchFor(x => x.CurrentFrameIndex, SyncFromSpriteEditorState);
            _appState.SpriteEditorState.WatchFor(x => x.FramesCount, SyncFromSpriteEditorState);
            _appState.SpriteEditorState.WatchFor(x => x.IsPlayingAnimation, SyncFromSpriteEditorState);
            _appState.SpriteEditorState.WatchFor(x => x.ShowOnionSkin, SyncFromSpriteEditorState);
            _appState.SpriteEditorState.WatchFor(x => x.FrameRate, SyncFromSpriteEditorState);

            messenger.Register<ProjectLoadedMessage>(this, _ =>
            {
                if (_appState.CurrentProject.CurrentEditedNode is Pix2dSprite sprite)
                {
                    _appState.SpriteEditorState.ShowOnionSkin = sprite.OnionSkinSettings.IsEnabled;
                }

                SyncFromSpriteEditorState();
            });
        }

        public ISpriteAnimationCommands SpriteAnimationCommands { get; }

        public IReadOnlyList<int> FrameRates { get; }

        public string FrameCounterText => $"{CurrentFrameIndex}/{FramesCount}";

        partial void OnShowOnionSkinChanged(bool? value)
        {
            if (_isSyncing)
                return;

            _appState.SpriteEditorState.ShowOnionSkin = value ?? false;
        }

        partial void OnFrameRateChanged(int value)
        {
            if (_isSyncing)
                return;

            _appState.SpriteEditorState.FrameRate = value;
        }

        private void SyncFromSpriteEditorState()
        {
            _isSyncing = true;
            CurrentFrameIndex = _appState.SpriteEditorState.CurrentFrameIndex;
            FramesCount = _appState.SpriteEditorState.FramesCount;
            IsPlayingAnimation = _appState.SpriteEditorState.IsPlayingAnimation;
            ShowOnionSkin = _appState.SpriteEditorState.ShowOnionSkin;
            FrameRate = _appState.SpriteEditorState.FrameRate;
            _isSyncing = false;
        }
    }
}