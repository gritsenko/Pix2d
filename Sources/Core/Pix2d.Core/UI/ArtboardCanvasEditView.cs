using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

/// <summary>
/// Apply / Cancel bar for an artboard canvas-edit session (Resize or Crop — see
/// <see cref="IArtboardObjectEditService"/>). Floats at the top-center of the canvas while the handle frame
/// is open and self-hides otherwise, so it replaces the General action bar for the duration of the session.
/// Selecting / moving / deleting artboards is not here — that is the General action bar (<see cref="ObjectActionsBarView"/>).
/// </summary>
public partial class ArtboardCanvasEditView(IMessenger messenger, IArtboardObjectEditService canvasEditService)
    : ViewBase<ArtboardCanvasEditView.State>(new State(messenger, canvasEditService))
{
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
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .VerticalAlignment(VerticalAlignment.Center)
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
                            .OnClick(() => state.Cancel())
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly IArtboardObjectEditService _service;

        [ObservableProperty]
        public partial bool IsActive { get; set; }

        [ObservableProperty]
        public partial string ModeTitle { get; set; } = "";

        public State(IMessenger messenger, IArtboardObjectEditService service)
        {
            _service = service;
            messenger.Register<ArtboardObjectEditStateChangedMessage>(this, m => Sync(m.IsActive, m.Mode));
            Sync(service.IsActive, service.Mode);
        }

        private void Sync(bool isActive, ArtboardObjectEditMode mode)
        {
            IsActive = isActive;
            ModeTitle = mode switch
            {
                ArtboardObjectEditMode.Resize => L("Resize"),
                ArtboardObjectEditMode.Crop => L("Crop"),
                _ => ""
            };
        }

        public void Confirm() => _service.ConfirmMode();
        public void Cancel() => _service.CancelMode();
    }
}
