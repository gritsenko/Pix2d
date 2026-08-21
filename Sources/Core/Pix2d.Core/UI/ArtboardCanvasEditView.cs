using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI;

/// <summary>
/// Action bar of an artboard canvas-edit session (Resize or Crop — see <see cref="IArtboardObjectEditService"/>).
/// Floats at the top-center of the canvas while the handle frame is open and self-hides otherwise, so it
/// replaces the General action bar for the duration of the session.
///
/// Besides Apply / Cancel it is the *keyboard* route into the frame the handles drag: width / height boxes
/// (and, for Resize, a scale percentage of the artboard's original size) write the same preview-only frame,
/// and a lock toggle drives its proportional constraint — dragging a handle keeps the ratio while the toggle
/// is on, and holding Shift inverts that for the gesture in flight.
///
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

    /// <summary>A bounded, spinner-less size box: the bar is a toolbar, not a dialog, and an unbounded input
    /// would let a typo become the canvas size (see the same note in <see cref="ResizeCanvasView"/>).</summary>
    private static NumericUpDown SizeBox(string tooltip) =>
        new NumericUpDown()
            .ShowButtonSpinner(false)
            .ClipValueToMinMax(true)
            .FormatString("N0")
            .Minimum(State.MinDimension)
            .Maximum(State.MaxDimension)
            .Width(70)
            .Height(34)
            .FontSize(13)
            .VerticalAlignment(VerticalAlignment.Center)
            .VerticalContentAlignment(VerticalAlignment.Center)
            .ToolTip_Tip(tooltip);

    private static TextBlock BoxLabel(string text) =>
        new TextBlock()
            .Classes("body11")
            .Text(text)
            .Margin(8, 0, 4, 0)
            .VerticalAlignment(VerticalAlignment.Center);

    protected override object Build(State state) =>
        // ScrollViewer outermost + width clamped to the window: the bar carries input boxes now, so on a
        // narrow window it scrolls horizontally instead of overflowing off-screen (same as ObjectActionsBarView).
        new ScrollViewer()
            .IsVisible(state, x => x.IsActive)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Hidden)
            .VerticalScrollBarVisibility(ScrollBarVisibility.Disabled)
            .ClampMaxWidthToViewport(StaticResources.Measures.PanelMargin * 2)
            .Content(
                new BlurPanel()
                    .HorizontalAlignment(HorizontalAlignment.Center)
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

                                BoxLabel(L("Width")),
                                SizeBox(L("Width"))
                                    .Value(state, x => x.FrameWidth, BindingMode.TwoWay),

                                BoxLabel(L("Height")),
                                SizeBox(L("Height"))
                                    .Value(state, x => x.FrameHeight, BindingMode.TwoWay),

                                // The lock the handles obey. Shift inverts it mid-drag, hence the tooltip.
                                new AppToggleButton()
                                    // IsChecked first: the AppButton setters below return the base type, and the
                                    // declarative IsChecked extension would then resolve to MenuItem's.
                                    .IsChecked(state, x => x.KeepAspect, BindingMode.TwoWay)
                                    .Content(new TextBlock()
                                        .Text("\xE72E")
                                        .FontFamily(StaticResources.Fonts.IconFontSegoe))
                                    .Label(L("Lock"))
                                    .ToolTip_Tip(L("Keep aspect ratio (hold Shift to invert)")),

                                // Scale is meaningful only where the pixels are scaled — Crop keeps them 1:1.
                                BoxLabel(L("Scale"))
                                    .IsVisible(state, x => x.IsResizeMode),
                                SizeBox(L("Scale"))
                                    .FormatString("0.#")
                                    .Minimum(State.MinScale)
                                    .Maximum(State.MaxScale)
                                    .IsVisible(state, x => x.IsResizeMode)
                                    .Value(state, x => x.ScalePercent, BindingMode.TwoWay),
                                new TextBlock()
                                    .Classes("body11")
                                    .Text("%")
                                    .Margin(4, 0, 0, 0)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .IsVisible(state, x => x.IsResizeMode),

                                ActionButton("✓", L("Apply"))
                                    .OnClick(() => state.Confirm()),

                                ActionButton("✕", L("Cancel"))
                                    .OnClick(() => state.Cancel())
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        /// <summary>The bounds the model enforces anyway (see <see cref="CanvasSize"/>), surfaced to the
        /// input boxes so the bar can never ask for a canvas the editor will refuse to allocate.</summary>
        public const decimal MinDimension = (decimal)CanvasSize.MinDimension;

        /// <inheritdoc cref="MinDimension"/>
        public const decimal MaxDimension = (decimal)CanvasSize.MaxDimension;

        /// <summary>Scale-box bounds. The size clamp above is the real guard — these only keep the
        /// percentage itself sane while it is being typed.</summary>
        public const decimal MinScale = 1m;

        /// <inheritdoc cref="MinScale"/>
        public const decimal MaxScale = 10000m;

        private readonly IArtboardObjectEditService _service;

        /// <summary>Guards the State ← service direction so a programmatic refresh does not re-enter the
        /// user-edit path (the standard sync-loop guard).</summary>
        private bool _isSyncing;

        /// <summary>The box whose value the user is currently typing into, while its edit is being pushed to
        /// the frame. That one box is left alone by the refresh that follows — rewriting the text under the
        /// caret would make a multi-digit number impossible to type; the other boxes must follow, being
        /// derived from the same frame.</summary>
        private string? _editedBox;

        private SKSize _originalSize;

        [ObservableProperty]
        public partial bool IsActive { get; set; }

        [ObservableProperty]
        public partial string ModeTitle { get; set; } = "";

        [ObservableProperty]
        public partial bool IsResizeMode { get; set; }

        [ObservableProperty]
        public partial decimal? FrameWidth { get; set; }

        [ObservableProperty]
        public partial decimal? FrameHeight { get; set; }

        /// <summary>Frame size as a percentage of the artboard's size when the session opened.</summary>
        [ObservableProperty]
        public partial decimal? ScalePercent { get; set; }

        [ObservableProperty]
        public partial bool KeepAspect { get; set; }

        public State(IMessenger messenger, IArtboardObjectEditService service)
        {
            _service = service;
            messenger.Register<ArtboardObjectEditStateChangedMessage>(this, m => Sync(m.IsActive, m.Mode));
            // Handle drags change the frame without touching this view — follow them so the boxes always
            // read what Apply would commit.
            messenger.Register<ArtboardObjectEditFrameChangedMessage>(this, _ => SyncFromFrame());
            Sync(service.IsActive, service.Mode);
        }

        private void Sync(bool isActive, ArtboardObjectEditMode mode)
        {
            IsActive = isActive;
            IsResizeMode = mode == ArtboardObjectEditMode.Resize;
            ModeTitle = mode switch
            {
                ArtboardObjectEditMode.Resize => L("Resize"),
                ArtboardObjectEditMode.Crop => L("Crop"),
                _ => ""
            };

            _originalSize = _service.OriginalSize;

            _isSyncing = true;
            KeepAspect = _service.KeepAspect; // per-mode default, reset by every Begin
            _isSyncing = false;

            SyncFromFrame();
        }

        /// <summary>Pulls the live frame into the boxes (all but the one being typed into).</summary>
        private void SyncFromFrame()
        {
            var frame = _service.FrameRect;
            if (frame.Width < 1 || frame.Height < 1)
                return;

            _isSyncing = true;

            if (_editedBox != nameof(FrameWidth))
                FrameWidth = (decimal)MathF.Round(frame.Width);

            if (_editedBox != nameof(FrameHeight))
                FrameHeight = (decimal)MathF.Round(frame.Height);

            if (_editedBox != nameof(ScalePercent))
                ScalePercent = _originalSize.Width >= 1
                    ? Math.Round((decimal)(frame.Width / _originalSize.Width) * 100m, 1)
                    : null;

            _isSyncing = false;
        }

        partial void OnFrameWidthChanged(decimal? value)
        {
            if (_isSyncing || !value.HasValue)
                return;

            var width = (float)value.Value;
            PushFrameSize(width, LinkedHeight(width), nameof(FrameWidth));
        }

        partial void OnFrameHeightChanged(decimal? value)
        {
            if (_isSyncing || !value.HasValue)
                return;

            var height = (float)value.Value;
            PushFrameSize(LinkedWidth(height), height, nameof(FrameHeight));
        }

        partial void OnScalePercentChanged(decimal? value)
        {
            if (_isSyncing || !value.HasValue || _originalSize.Width < 1 || _originalSize.Height < 1)
                return;

            // A percentage is proportional by definition, so it ignores the lock toggle.
            var scale = (float)value.Value / 100f;
            PushFrameSize(_originalSize.Width * scale, _originalSize.Height * scale, nameof(ScalePercent));
        }

        partial void OnKeepAspectChanged(bool value)
        {
            if (_isSyncing)
                return;

            _service.KeepAspect = value;
        }

        /// <summary>The height a typed width implies: the *frame's current* ratio, not the artboard's
        /// original, so a locked keyboard edit preserves what is on screen after an unlocked drag.</summary>
        private float LinkedHeight(float width)
        {
            var frame = _service.FrameRect; // still the pre-edit frame — nothing is pushed yet
            return KeepAspect && frame.Width >= 1 && frame.Height >= 1
                ? MathF.Round(width * frame.Height / frame.Width)
                : (float)(FrameHeight ?? 0);
        }

        /// <inheritdoc cref="LinkedHeight"/>
        private float LinkedWidth(float height)
        {
            var frame = _service.FrameRect;
            return KeepAspect && frame.Width >= 1 && frame.Height >= 1
                ? MathF.Round(height * frame.Width / frame.Height)
                : (float)(FrameWidth ?? 0);
        }

        private void PushFrameSize(float width, float height, string editedBox)
        {
            _editedBox = editedBox;
            try
            {
                _service.SetFrameSize(new SKSize(width, height));
                // Explicit re-read rather than relying on the change message: the service clamps and rounds,
                // and a value that ends up identical to the current frame raises nothing at all.
                SyncFromFrame();
            }
            finally
            {
                _editedBox = null;
            }
        }

        public void Confirm() => _service.ConfirmMode();
        public void Cancel() => _service.CancelMode();
    }
}
