using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
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

    /// <summary>
    /// Opens <paramref name="flyout"/> when the button is held down, leaving a plain click to toggle.
    /// This is the gesture issue #214 asked for, and it is the only one available: Pix2d binds the right
    /// mouse button to the eyedropper app-wide, so the button's <c>ContextFlyout</c> never gets a
    /// <c>ContextRequested</c> on desktop (verified on a running build — right-clicking it just switched
    /// the tool). Doing it by hand rather than through the platform gesture also makes mouse, pen and
    /// touch behave identically.
    /// </summary>
    private static void AttachLongPress(Control button, FlyoutBase flyout, State state)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };

        timer.Tick += (_, _) =>
        {
            timer.Stop();
            // If the release still raises Click it would toggle the setting the user was only trying to
            // configure, so the state drops that one change.
            state.SuppressNextSymmetryToggle = true;
            flyout.ShowAt(button);
        };

        button.AddHandler(InputElement.PointerPressedEvent, (_, _) =>
            {
                // Disarm on every press, not only when the suppressed Click arrives: opening the flyout
                // takes the pointer capture, so the release that ends a long press does not always raise
                // Click — and a flag left armed would then swallow the user's *next*, deliberate click.
                // (Seen on a running build: after one long press the toggle stopped responding.)
                state.SuppressNextSymmetryToggle = false;
                timer.Start();
            },
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        button.AddHandler(InputElement.PointerReleasedEvent, (_, _) => timer.Stop(),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        button.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => timer.Stop(),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

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
        // ScrollViewer is the OUTER element (bounded by the Stretch placement in MainView) so the action
        // pill scrolls horizontally instead of overflowing off-screen on a narrow phone-portrait window;
        // the BlurPanel pill stays content-sized and centred when everything fits.
        new ScrollViewer()
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
            // Bound to the window width so the action pill scrolls instead of overflowing on a narrow
            // screen (its grid column can't bound it — the bottom side-panels contaminate the column).
            .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
            .Content(
                new BlurPanel()
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

                            // SYMMETRY — a click toggles it (the old Mirror X gesture), a press-and-hold
                            // opens the axes + angle settings (see AttachLongPress). One button replaces
                            // the two Mirror toggles: X, Y and X+Y are now presets inside the same model,
                            // so a second toolbar toggle could only express two of the states.
                            new AppToggleButton()
                                .Classes(ButtonClass)
                                .IsChecked(state, x => x.IsSymmetryEnabled, BindingMode.TwoWay)
                                .Content(new PathIcon()
                                    .With(IconStyle)
                                    .Data(Geometry.Parse(
                                        "M8,14V0H9V14Zm9-2H11V2h6V12h0Zm-5-1h4V3H12ZM0,12V2H6V12Z")))
                                .Label(L("Symmetry"))
                                .ToolTip_Tip(L("Symmetry (hold for settings)"))
                                .ContextFlyout(
                                    new Flyout()
                                        .Ref(out var symmetryFlyout)
                                        .Placement(PlacementMode.Bottom)
                                        .Content(ViewFactory.Create<SymmetrySettingsView>())
                                )
                                .With(b => AttachLongPress(b, symmetryFlyout, state)),

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
        public partial bool IsSymmetryEnabled { get; set; }

        [ObservableProperty]
        public partial bool LockAxis { get; set; }

        // The toggle mirrors AppState, so turning symmetry on from the settings flyout (or off from a
        // future shortcut) lights the toolbar button too; _syncing keeps the echo out of the user path.
        private bool _syncing;

        /// <summary>Set by the long-press gesture: the Click that ends the hold must not flip the setting.</summary>
        public bool SuppressNextSymmetryToggle { get; set; }

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
            SyncSymmetry();

            _appState.SpriteEditorState.WatchFor(x => x.IsPlayingAnimation, () => IsAnimationPlaying = _appState.SpriteEditorState.IsPlayingAnimation);
            _appState.SpriteEditorState.WatchFor(x => x.Symmetry, SyncSymmetry);
        }

        private void SyncSymmetry()
        {
            _syncing = true;
            IsSymmetryEnabled = _appState.SpriteEditorState.Symmetry.IsEnabled;
            _syncing = false;
        }

        public SpriteEditCommands SpriteEditCommands { get; }

        public ViewCommands ViewCommands { get; }

        public EditCommands EditCommands { get; }

        public bool CanEdit => !IsAnimationPlaying;

        partial void OnIsSymmetryEnabledChanged(bool value)
        {
            if (_syncing) return;

            if (SuppressNextSymmetryToggle)
            {
                SuppressNextSymmetryToggle = false;
                SyncSymmetry(); // put the visual back where the app state still is
                return;
            }

            // Turning it on for the first time lands on the classic single vertical axis; after that the
            // toggle only flips IsEnabled, so the user's axis count / angle / centre survive an off/on.
            var current = _appState.SpriteEditorState.Symmetry;
            _drawingService.SetSymmetry(
                value && current.AxisCount <= 1 && current.AngleDegrees == 0 && !current.IsEnabled
                    ? Primitives.Drawing.SymmetrySettings.MirrorX(current.Center)
                    : current with { IsEnabled = value });
        }

        partial void OnLockAxisChanged(bool value)
        {
            _snappingService.ForceAspectLock = value;
        }
    }
}