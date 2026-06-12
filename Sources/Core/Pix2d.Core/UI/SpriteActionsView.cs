using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Messages;
using Pix2d.Services;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

/// <summary>
/// Contextual toolbar for the "edit sprite as object" mode (see <see cref="ArtboardObjectEditService"/>).
/// Floats at the top-center of the canvas while an artboard is selected and swaps its button set by sub-mode:
/// <list type="bullet">
/// <item><b>Move</b> (default after selection — the artboard is dragged only by its name label): Resize, Crop,
/// Set name, and Done (exits the session, duplicating a click outside the artboard).</item>
/// <item><b>Resize / Crop</b>: a title plus Apply (commit) and Cancel (discard the preview, back to Move).</item>
/// </list>
/// </summary>
public partial class SpriteActionsView(IMessenger messenger, ArtboardObjectEditService artboardObjectEditService)
    : ViewBase<SpriteActionsView.State>(new State(messenger, artboardObjectEditService))
{
    private static void IconStyle(PathIcon icon) => icon.Width(16).Height(16);

    // Size/margins come from the default `AppButton` style (44×44, Margin 6) so the bar matches the other
    // toolbars — don't pin Width/Height or add StackPanel spacing here.
    private static AppButton ActionButton(object content, string label) =>
        new AppButton()
            .Content(content)
            .Label(label)
            .ToolTip_Tip(label);

    protected override object Build(State state) =>
        new BlurPanel()
            .IsVisible(state, x => x.IsActive)
            .Content(
                new Grid()
                    .Children(
                        // Move mode — select & drag-by-label; pick a sub-mode or finish.
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .IsVisible(state, x => x.IsMoveMode)
                            .Children(
                                ActionButton(
                                        new PathIcon().With(IconStyle).Data(Geometry.Parse(
                                            "M 3 1 L 3 3 L 1 3 L 1 4 L 3 4 L 3 12 L 11 12 L 11 14 L 12 14 L 12 12 L 14 12 L 14 11 L 4.707031 11 L 11 4.707031 L 11 10 L 12 10 L 12 3.707031 L 13.355469 2.351563 L 12.644531 1.648438 L 11.292969 3 L 5 3 L 5 4 L 10.292969 4 L 4 10.292969 L 4 1 Z ")),
                                        L("Resize"))
                                    .OnClick(() => state.EnterResize()),

                                ActionButton(
                                        new PathIcon().With(IconStyle).Data(StaticResources.Icons.CropIcon),
                                        L("Crop"))
                                    .OnClick(() => state.EnterCrop()),

                                ActionButton("✎", L("Set name"))
                                    .OnClick(() => state.SetName()),

                                ActionButton("✓", L("Done"))
                                    .OnClick(() => state.Done())
                            ),

                        // Resize / Crop mode — commit or discard the framed change.
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .IsVisible(state, x => x.IsEditingSize)
                            .Children(
                                new TextBlock()
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(8, 0, 8, 0)
                                    .FontSize(14)
                                    .Foreground(StaticResources.Brushes.ForegroundBrush)
                                    .Text(state, x => x.ModeTitle),

                                ActionButton("✓", L("Apply"))
                                    .OnClick(() => state.Confirm()),

                                ActionButton("✕", L("Cancel"))
                                    .OnClick(() => state.CancelMode())
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly ArtboardObjectEditService _service;

        [ObservableProperty]
        public partial bool IsActive { get; set; }

        [ObservableProperty]
        public partial bool IsMoveMode { get; set; }

        [ObservableProperty]
        public partial bool IsEditingSize { get; set; }

        [ObservableProperty]
        public partial string ModeTitle { get; set; } = "";

        public State(IMessenger messenger, ArtboardObjectEditService service)
        {
            _service = service;
            messenger.Register<ArtboardObjectEditStateChangedMessage>(this, m => Sync(m.IsActive, m.Mode));
            Sync(service.IsActive, service.Mode);
        }

        private void Sync(bool isActive, ArtboardObjectEditMode mode)
        {
            IsActive = isActive;
            IsMoveMode = isActive && mode == ArtboardObjectEditMode.Move;
            IsEditingSize = isActive && mode != ArtboardObjectEditMode.Move;
            ModeTitle = mode switch
            {
                ArtboardObjectEditMode.Resize => L("Resize"),
                ArtboardObjectEditMode.Crop => L("Crop"),
                _ => ""
            };
        }

        public void EnterResize() => _service.EnterResizeMode();
        public void EnterCrop() => _service.EnterCropMode();
        public void Confirm() => _service.ConfirmMode();
        public void CancelMode() => _service.CancelMode();
        public void Done() => _service.Exit();
        public void SetName() => _ = _service.RenameAsync();
    }
}
