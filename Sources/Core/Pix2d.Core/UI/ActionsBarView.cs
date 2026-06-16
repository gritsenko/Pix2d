using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.Plugins.Sprite.Commands;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

public partial class ActionsBarView(AppState appState, ICommandService commandService, IDrawingService drawingService, ISnappingService snappingService)
    : ViewBase<ActionsBarView.State>(new State(appState, commandService, drawingService, snappingService))
{
    public const string ButtonClass = "actions-bar-button";

    private static void IconStyle(PathIcon icon) => icon
        .Width(16)
        .Height(16);

    protected override StyleGroup BuildStyles() =>
    [
        new Style<Button>(_ => VisualStates.Wide().OfType<ActionsBarView>().Descendant().Is<AppButton>())
            .Width(58)
            .Height(58),
        new Style<Button>(_ => VisualStates.Wide().OfType<ActionsBarView>().Descendant().Is<AppButton>())
            .Width(48)
            .Height(48),
        new Style<TextBlock>(_ =>
                VisualStates.Narrow().OfType<ActionsBarView>().Descendant().Is<AppButton>().Descendant()
                    .OfType<TextBlock>())
            .FontSize(9),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            // Tighten the action toolbar: no gap between buttons + smaller corner radius.
            new Style<AppButton>(s => s.Is<AppButton>())
                .Margin(0),
            new Style<Button>(s => s.OfType<Button>().Class("app-button"))
                .CornerRadius(StaticResources.Measures.CompactButtonCornerRadius),
        }
    ];

    protected override object Build(State state) =>
        new BlurPanel().Content(
            new ScrollViewer()
                .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .Content(
                    new StackPanel()
                        .Orientation(Orientation.Horizontal)
                        .Children(

                            new AppButton()
                                .Classes(ButtonClass)
                                .Command(state.SpriteEditCommands.Rotate90)
                                .IsEnabled(state, x => x.CanEdit)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M 8 0 L 8 2 C 4.691406 2 2 4.691406 2 8 C 2 11.308594 4.691406 14 8 14 C 11.128906 14 13.730469 11.613281 14 8.542969 C 14.019531 8.363281 13.9375 8.183594 13.789063 8.082031 C 13.644531 7.976563 13.449219 7.957031 13.285156 8.035156 C 13.121094 8.113281 13.011719 8.277344 13 8.457031 C 12.78125 11.011719 10.621094 13 8 13 C 5.230469 13 3 10.769531 3 8 C 3 5.230469 5.230469 3 8 3 L 8 5 L 11 2.5 Z M 12.5 4 C 12.222656 4 12 4.222656 12 4.5 C 12 4.777344 12.222656 5 12.5 5 C 12.777344 5 13 4.777344 13 4.5 C 13 4.222656 12.777344 4 12.5 4 Z M 13.5 6 C 13.222656 6 13 6.222656 13 6.5 C 13 6.777344 13.222656 7 13.5 7 C 13.777344 7 14 6.777344 14 6.5 C 14 6.222656 13.777344 6 13.5 6 Z ")))
                                .Label(L("Rotate"))
                                .ToolTip_Tip(L(state.SpriteEditCommands.Rotate90.Tooltip)),

                            new AppButton()
                                .Classes(ButtonClass)
                                .Command(state.SpriteEditCommands.FlipHorizontal)
                                .IsEnabled(state, x => x.CanEdit)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M 8 1 L 8 13 L 14 13 Z M 7 1.007813 L 1.007813 13 L 7 13 Z M 6 5.242188 L 6 12 L 2.625 12 Z ")))
                                .Label(L("Flip X"))
                                .ToolTip_Tip(L(state.SpriteEditCommands.FlipHorizontal.Tooltip)),

                            new AppButton()
                                .Classes(ButtonClass)
                                .Command(state.SpriteEditCommands.FlipVertical)
                                .IsEnabled(state, x => x.CanEdit)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M 14 1 L 2 7 L 14 7 Z M 2 8 L 14 14 L 14 8 Z M 6.234375 9 L 13 9 L 13 12.382813 Z ")))
                                .Label(L("Flip Y"))
                                .ToolTip_Tip(L(state.SpriteEditCommands.FlipVertical.Tooltip)),

                            // MIRROR X
                            new AppToggleButton()
                                .Classes(ButtonClass)
                                .IsChecked(state, x => x.MirrorX, BindingMode.TwoWay)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M8,14V0H9V14Zm9-2H11V2h6V12h0Zm-5-1h4V3H12ZM0,12V2H6V12Z")))
                                .Label(L("Mirror X"))
                                .ToolTip_Tip(L("Mirror X")),

                            // MIRROR Y
                            new AppToggleButton()
                                .Classes(ButtonClass)
                                .IsChecked(state, x => x.MirrorY, BindingMode.TwoWay)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M-393.477 -548.726L-403.477 -548.726L-403.477 -542.726L-393.477 -542.726L-393.477 -548.726L-393.477 -548.726ZM-391.477 -540.726L-405.477 -540.726L-405.477 -539.726L-391.477 -539.726L-391.477 -540.726L-391.477 -540.726ZM-403.478 -537.726L-403.478 -531.726L-393.478 -531.726L-393.478 -537.726L-403.478 -537.726L-403.478 -537.726ZM-402.477 -532.726L-402.477 -536.726L-394.477 -536.726L-394.477 -532.726L-402.477 -532.726L-402.477 -532.726Z")))
                                .Label(L("Mirror Y"))
                                .ToolTip_Tip(L("Mirror Y")),

                            //Grid settings
                            new AppButton()
                                .Classes(ButtonClass)
                                .Ref(out var gridButton)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(StaticResources.Icons.GridIcon)
                                )
                                .Label(L("Grid"))
                                .ToolTip_Tip(L("Grid settings"))
                                .ContextFlyout(
                                    new Flyout()
                                        .Ref(out var flyout)
                                        .Placement(PlacementMode.Bottom)
                                        .Content(ViewFactory.Create<GridSettingsView>())
                                )
                                .OnClick(() => flyout.ShowAt(gridButton)),

                            //Lock axis
                            new AppToggleButton()
                                .Classes(ButtonClass)
                                .IsChecked(state, x => x.LockAxis, BindingMode.TwoWay)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M19,20H1V3H0L1,1H1l.5-1L3,3H2V19H19V18l3,1.5L19,21Zm-5-3H4V8H5a4,4,0,0,1,8,0h1v9h0ZM7,13a2,2,0,1,0,2-2A2,2,0,0,0,7,13ZM6,8h6A3,3,0,0,0,6,8Z")))
                                .Label(L("Lock axis"))
                                .ToolTip_Tip(L("Lock axis")),

                            //Import
                            new AppButton()
                                .Classes(ButtonClass)
                                .Command(state.EditCommands.Import)
                                .IsEnabled(state, x => x.CanEdit)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M 2.5 1 C 1.675781 1 1 1.675781 1 2.5 L 1 12.5 C 1 13.324219 1.675781 14 2.5 14 L 12.5 14 C 13.324219 14 14 13.324219 14 12.5 L 14 10 L 13 10 L 13 12.5 C 13 12.78125 12.78125 13 12.5 13 L 2.5 13 C 2.21875 13 2 12.78125 2 12.5 L 2 2.5 C 2 2.21875 2.21875 2 2.5 2 L 12.5 2 C 12.78125 2 13 2.21875 13 2.5 L 13 5 L 14 5 L 14 2.5 C 14 1.675781 13.324219 1 12.5 1 Z M 8.273438 4.023438 L 4.792969 7.5 L 8.273438 10.980469 L 8.976563 10.269531 L 6.707031 8 L 14 8 L 14 7 L 6.707031 7 L 8.976563 4.726563 Z ")))
                                .Label(L("Import"))
                                .ToolTip_Tip(L(state.EditCommands.Import.Tooltip)),

                            //Resize
                            new AppButton()
                                .Classes(ButtonClass)
                                .Command(state.ViewCommands.ToggleCanvasSizePanelCommand)
                                .IsEnabled(state, x => x.CanEdit)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M 3 1 L 3 3 L 1 3 L 1 4 L 3 4 L 3 12 L 11 12 L 11 14 L 12 14 L 12 12 L 14 12 L 14 11 L 4.707031 11 L 11 4.707031 L 11 10 L 12 10 L 12 3.707031 L 13.355469 2.351563 L 12.644531 1.648438 L 11.292969 3 L 5 3 L 5 4 L 10.292969 4 L 4 10.292969 L 4 1 Z ")))
                                .Label(L("Resize"))
                                .ToolTip_Tip(L("Image/Canvas size"))

                        )
                )
        );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IDrawingService _drawingService;
        private readonly ISnappingService _snappingService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        public partial bool IsAnimationPlaying { get; set; }

        [ObservableProperty]
        public partial bool MirrorX { get; set; }

        [ObservableProperty]
        public partial bool MirrorY { get; set; }

        [ObservableProperty]
        public partial bool LockAxis { get; set; }

        public State(AppState appState, ICommandService commandService, IDrawingService drawingService, ISnappingService snappingService)
        {
            _appState = appState;
            _drawingService = drawingService;
            _snappingService = snappingService;

            SpriteEditCommands = commandService.GetCommandList<SpriteEditCommands>()!;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;
            EditCommands = commandService.GetCommandList<EditCommands>()!;

            IsAnimationPlaying = _appState.SpriteEditorState.IsPlayingAnimation;
            LockAxis = _snappingService.ForceAspectLock;

            _appState.SpriteEditorState.WatchFor(x => x.IsPlayingAnimation, () => IsAnimationPlaying = _appState.SpriteEditorState.IsPlayingAnimation);
        }

        public SpriteEditCommands SpriteEditCommands { get; }

        public ViewCommands ViewCommands { get; }

        public EditCommands EditCommands { get; }

        public bool CanEdit => !IsAnimationPlaying;

        partial void OnMirrorXChanged(bool value)
        {
            _drawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Horizontal, value);
        }

        partial void OnMirrorYChanged(bool value)
        {
            _drawingService.SetMirrorMode(Primitives.Drawing.MirrorMode.Vertical, value);
        }

        partial void OnLockAxisChanged(bool value)
        {
            _snappingService.ForceAspectLock = value;
        }
    }
}