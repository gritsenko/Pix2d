using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

/// <summary>
/// Action bar of the General (objects) context — the artboard-level counterpart of the Sprite context's
/// <see cref="ActionsBarView"/>. Floats at the top-center of the canvas whenever the General context is
/// active and the top bar's Tools toggle is on, and steps aside while an artboard canvas-edit session runs
/// (<see cref="ArtboardCanvasEditView"/> owns the screen then).
///
/// Every button acts on the current object selection, which is driven by <c>ObjectManipulationTool</c>:
/// Delete / z-order work on any number of artboards, Arrange needs at least two, and Resize / Crop / Rename
/// need exactly one (they change a single artboard's canvas). Buttons disable themselves accordingly —
/// <c>ICommandService.ExecuteCommandAsync</c> does not gate on <c>EditContextType</c>, so the view is the
/// guardrail for anything invoked by click.
/// </summary>
public partial class ObjectActionsBarView(
    AppState appState,
    IMessenger messenger,
    ICommandService commandService,
    IArtboardObjectEditService canvasEditService)
    : ViewBase<ObjectActionsBarView.State>(new State(appState, messenger, commandService, canvasEditService))
{
    public const string ButtonClass = "actions-bar-button";

    private static void IconStyle(PathIcon icon) => icon.Width(16).Height(16);

    protected override StyleGroup BuildStyles() =>
    [
        new Style<Button>(_ => VisualStates.Wide().OfType<ObjectActionsBarView>().Descendant().Is<AppButton>())
            .Width(58)
            .Height(58),
        new Style<TextBlock>(_ =>
                VisualStates.Narrow().OfType<ObjectActionsBarView>().Descendant().Is<AppButton>().Descendant()
                    .OfType<TextBlock>())
            .FontSize(9),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            // Tighten the action toolbar: no gap between buttons + smaller corner radius (matches ActionsBarView).
            new Style<AppButton>(s => s.Is<AppButton>())
                .Margin(0),
            new Style<Button>(s => s.OfType<Button>().Class("app-button"))
                .CornerRadius(StaticResources.Measures.CompactButtonCornerRadius),
        }
    ];

    protected override object Build(State state) =>
        // ScrollViewer outermost + width clamped to the window, so the pill scrolls horizontally instead of
        // overflowing off-screen on a narrow window (same reasoning as ActionsBarView).
        new ScrollViewer()
            .IsVisible(state, x => x.IsVisible)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
            .Content(
                new BlurPanel()
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                // RESIZE — scales the artboard's pixel content to a new canvas size.
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .IsEnabled(state, x => x.HasSingleArtboard)
                                    .Content(new PathIcon()
                                        .With(IconStyle)
                                        .Data(Geometry.Parse(
                                            "M 3 1 L 3 3 L 1 3 L 1 4 L 3 4 L 3 12 L 11 12 L 11 14 L 12 14 L 12 12 L 14 12 L 14 11 L 4.707031 11 L 11 4.707031 L 11 10 L 12 10 L 12 3.707031 L 13.355469 2.351563 L 12.644531 1.648438 L 11.292969 3 L 5 3 L 5 4 L 10.292969 4 L 4 10.292969 L 4 1 Z ")))
                                    .Label(L("Resize"))
                                    .ToolTip_Tip(L("Resize artboard"))
                                    .OnClick(() => state.BeginResize()),

                                // CROP — changes the canvas without scaling the content.
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .IsEnabled(state, x => x.HasSingleArtboard)
                                    .Content(new PathIcon()
                                        .With(IconStyle)
                                        .Data(StaticResources.Icons.CropIcon)
                                        .RenderTransform(new ScaleTransform(1, -1)))
                                    .Label(L("Crop"))
                                    .ToolTip_Tip(L("Crop artboard"))
                                    .OnClick(() => state.BeginCrop()),

                                // RENAME — the name shown by the always-on artboard label.
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .IsEnabled(state, x => x.HasSingleArtboard)
                                    .Content("✎")
                                    .Label(L("Rename"))
                                    .ToolTip_Tip(L("Set name"))
                                    .OnClick(() => state.Rename()),

                                // ARRANGE — repacks the selected artboards into a dense grid, grouped by
                                // their shared name prefixes.
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .Command(state.ArrangeCommands.Arrange)
                                    .IsEnabled(state, x => x.CanArrange)
                                    .Content(new PathIcon()
                                        .With(IconStyle)
                                        .Data(StaticResources.Icons.GridIcon))
                                    .Label(L("Arrange"))
                                    .ToolTip_Tip(L(state.ArrangeCommands.Arrange.Tooltip)),

                                // Z-ORDER
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .Command(state.ArrangeCommands.BringForward)
                                    .IsEnabled(state, x => x.HasSelection)
                                    .Content(new TextBlock()
                                        .Text("\xE74A")
                                        .FontFamily(StaticResources.Fonts.IconFontSegoe))
                                    .Label(L("Up"))
                                    .ToolTip_Tip(L(state.ArrangeCommands.BringForward.Tooltip)),

                                new AppButton()
                                    .Classes(ButtonClass)
                                    .Command(state.ArrangeCommands.SendBackward)
                                    .IsEnabled(state, x => x.HasSelection)
                                    .Content(new TextBlock()
                                        .Text("\xE74B")
                                        .FontFamily(StaticResources.Fonts.IconFontSegoe))
                                    .Label(L("Down"))
                                    .ToolTip_Tip(L(state.ArrangeCommands.SendBackward.Tooltip)),

                                // DELETE — confirmed + undoable (IEditService.DeleteSelectedObjectsAsync).
                                new AppButton()
                                    .Classes(ButtonClass)
                                    .Command(state.EditCommands.Delete)
                                    .IsEnabled(state, x => x.HasSelection)
                                    .Content(new PathIcon()
                                        .With(IconStyle)
                                        .Data(Geometry.Parse(
                                            "M 6.496094 1 C 5.675781 1 5 1.675781 5 2.496094 L 5 3 L 2 3 L 2 4 L 3 4 L 3 12.5 C 3 13.324219 3.675781 14 4.5 14 L 10.5 14 C 11.324219 14 12 13.324219 12 12.5 L 12 4 L 13 4 L 13 3 L 10 3 L 10 2.496094 C 10 1.675781 9.324219 1 8.503906 1 Z M 6.496094 2 L 8.503906 2 C 8.785156 2 9 2.214844 9 2.496094 L 9 3 L 6 3 L 6 2.496094 C 6 2.214844 6.214844 2 6.496094 2 Z M 4 4 L 11 4 L 11 12.5 C 11 12.78125 10.78125 13 10.5 13 L 4.5 13 C 4.21875 13 4 12.78125 4 12.5 Z M 5 5 L 5 12 L 6 12 L 6 5 Z M 7 5 L 7 12 L 8 12 L 8 5 Z M 9 5 L 9 12 L 10 12 L 10 5 Z ")))
                                    .Label(L("Delete"))
                                    .ToolTip_Tip(L(state.EditCommands.Delete.Tooltip))
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IArtboardObjectEditService _canvasEditService;

        [ObservableProperty]
        public partial bool IsVisible { get; set; }

        [ObservableProperty]
        public partial bool HasSelection { get; set; }

        [ObservableProperty]
        public partial bool HasSingleArtboard { get; set; }

        [ObservableProperty]
        public partial bool CanArrange { get; set; }

        public State(AppState appState, IMessenger messenger, ICommandService commandService,
            IArtboardObjectEditService canvasEditService)
        {
            _appState = appState;
            _canvasEditService = canvasEditService;

            // Both lists come from the command service — the only instances that have their
            // ICommandService/IServiceProvider injected (see the note in EditCommands).
            EditCommands = commandService.GetCommandList<EditCommands>()!;
            ArrangeCommands = commandService.GetCommandList<ArrangeCommands>()!;

            Sync();

            // Selection has no watchable state property (ProjectState.Selection is a plain field), so the
            // messenger is the signal — SelectionService raises it on every selection change/invalidation.
            messenger.Register<NodesSelectedMessage>(this, _ => Sync());
            messenger.Register<ArtboardObjectEditStateChangedMessage>(this, _ => Sync());
            _appState.UiState.WatchFor(x => x.ShowExtraTools, Sync);
            // WatchForCurrentProject (not WatchFor) so the binding survives a project-tab switch.
            _appState.WatchForCurrentProject(x => x.CurrentContextType, Sync);
        }

        public EditCommands EditCommands { get; }

        public ArrangeCommands ArrangeCommands { get; }

        private void Sync()
        {
            var selected = _appState.CurrentProject.Selection?.Nodes ?? [];

            // Follows the top bar's Tools toggle exactly like the Sprite context's ActionsBarView
            // (MainViewModel.ShowSpriteExtraTools) — one switch hides the action bar of either context.
            // Also hidden while a canvas-edit frame is open: ArtboardCanvasEditView's Apply/Cancel bar
            // takes over then (and it ignores the toggle, being the only way out of the session).
            IsVisible = _appState.CurrentProject.CurrentContextType == EditContextType.General
                        && _appState.UiState.ShowExtraTools
                        && !_canvasEditService.IsActive;

            HasSelection = selected.Length > 0;
            HasSingleArtboard = selected.Length == 1 && selected[0] is Pix2dSprite;
            CanArrange = selected.OfType<Pix2dSprite>().Count() > 1;
        }

        private Pix2dSprite? SelectedArtboard =>
            (_appState.CurrentProject.Selection?.Nodes ?? []).OfType<Pix2dSprite>().FirstOrDefault();

        public void BeginResize()
        {
            if (SelectedArtboard is { } sprite)
                _canvasEditService.Begin(sprite, ArtboardObjectEditMode.Resize);
        }

        public void BeginCrop()
        {
            if (SelectedArtboard is { } sprite)
                _canvasEditService.Begin(sprite, ArtboardObjectEditMode.Crop);
        }

        public void Rename()
        {
            if (SelectedArtboard is { } sprite)
                _ = _canvasEditService.RenameAsync(sprite);
        }
    }
}
