using Pix2d.Abstract.Commands;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.Animation;

public class AnimationControlsView : LocalizedComponentBase
{

    [Inject] public ICommandService CommandService { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;

    private SpriteEditorState SpriteEditorState => AppState.SpriteEditorState;
    private ISpriteAnimationCommands SpriteAnimationCommands =>
        CommandService.GetCommandList<ISpriteAnimationCommands>()!;

    protected override void OnAfterInitialized()
    {
        SpriteEditorState.WatchFor(s => s.CurrentFrameIndex, StateHasChanged);
        SpriteEditorState.WatchFor(s => s.FramesCount, StateHasChanged);
        Messenger.Register<ProjectLoadedMessage>(this, plm =>
        {
            if (AppState.CurrentProject.CurrentEditedNode is Pix2dSprite sprite)
            {
                SpriteEditorState.ShowOnionSkin = sprite.OnionSkinSettings.IsEnabled;
            }
            StateHasChanged();
        });
    }

    protected override object Build()=>
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
                            .Command(SpriteAnimationCommands.Stop)
                            .Content("\xe92e")

                            .Classes("anim-btn"),
                        new Button()
                            .Classes("anim-btn")
                            .Command(SpriteAnimationCommands.PrevFrame)
                            .Content("\xe92f")

                            .Classes("anim-btn"),
                        new ToggleButton()
                            .Classes("anim-btn")
                            .IsChecked(SpriteEditorState.IsPlayingAnimation)
                            .Content(SpriteEditorState.IsPlayingAnimation)
                            .OnClick(_ => SpriteAnimationCommands.TogglePlay.Execute())
                            .ContentTemplate(new FuncDataTemplate<bool>((v, _) =>
                                new TextBlock()
                                    .Text(v ? "\xe92c" : "\xe92d")))

                            .Classes("anim-btn"),
                        new Button()
                            .Command(SpriteAnimationCommands.NextFrame)
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
                                    .Text(()=>$"{SpriteEditorState.CurrentFrameIndex}/{SpriteEditorState.FramesCount}")
                                    .VerticalAlignment(VerticalAlignment.Center),

                                new Button()
                                    .Command(SpriteAnimationCommands.AddFrame)
                                    .Content("\xe920")
                                    .ToolTip("Add frame")
                                    .Classes("anim-btn"),
                                new Button()
                                    .Command(SpriteAnimationCommands.DuplicateFrame)
                                    .Content("\xe928")
                                    .ToolTip("Duplicate frame")
                                    .Classes("anim-btn"),
                                new Button()
                                    .Command(SpriteAnimationCommands.DeleteFrame)
                                    .Content("\xe929")
                                    .ToolTip("Delete frame")
                                    .Classes("anim-btn"),

                                //ONION SKIN BUTTON
                                new ToggleButton()
                                    .Classes("anim-btn")
                                    .VerticalContentAlignment(VerticalAlignment.Center)
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .IsChecked(() => SpriteEditorState.ShowOnionSkin, v => SpriteEditorState.ShowOnionSkin = v ?? false)
                                    .ToolTip("Onion skin")
                                    .Content(new TextBlock()
                                        .Text("\xe92b")
                                        .Padding(4)),

                                //FPS SELECTOR
                                new TextBlock()
                                    .Margin(left: 8)
                                    .Text("Fps")
                                    .VerticalAlignment(VerticalAlignment.Center),
                                new ComboBox()
                                    .ItemsSource(() => SpriteEditorState.FrameRates)
                                    .Margin(8, 0)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .SelectedItem(()=>SpriteEditorState.FrameRate, v => SpriteEditorState.FrameRate = (int)(v ?? 0))
                            ]))
            ]);

}