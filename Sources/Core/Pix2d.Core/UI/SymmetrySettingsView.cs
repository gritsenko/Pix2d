using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Primitives.Drawing;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI;

/// <summary>
/// Settings for the drawing symmetry, opened from the Symmetry toolbar button's context flyout
/// (right-click on desktop, long-press on touch — issue #214 asked for exactly that gesture).
///
/// <para>The three presets and the two sliders edit the same <see cref="SymmetrySettings"/>: X is one axis
/// at 0°, Y is one axis at 90°, X+Y is two axes at 0°. Anything else — 3 axes at 30°, 6 axes at 0° — is the
/// radial symmetry from issue #23's discussion, reachable only through the sliders.</para>
/// </summary>
public partial class SymmetrySettingsView(AppState appState, IDrawingService drawingService)
    : ViewBase<SymmetrySettingsView.State>(new State(appState, drawingService))
{
    private const string PresetButtonClass = "symmetry-preset";

    protected override StyleGroup BuildStyles() =>
    [
        new Style<ToggleButton>(s => s.OfType<ToggleButton>().Class(PresetButtonClass))
            .Height(32)
            .Margin(0, 0, 4, 0)
            .HorizontalContentAlignment(HorizontalAlignment.Center)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius),
    ];

    protected override object Build(State state) =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Child(
                new StackPanel()
                    .Margin(12)
                    .Width(260)
                    .Children(
                        new Grid()
                            .Cols("*,Auto")
                            .Children(
                                new TextBlock()
                                    .Classes("body11")
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Text(L("Symmetry").ToUpperInvariant()),
                                new ToggleSwitch().Col(1)
                                    .IsChecked(state, x => x.IsEnabled, BindingMode.TwoWay)
                            ),

                        new Grid()
                            .Margin(0, 8, 0, 0)
                            .Cols("*,*,*")
                            .Children(
                                new ToggleButton()
                                    .Classes(PresetButtonClass)
                                    .Content("X")
                                    .ToolTip_Tip(L("One vertical axis"))
                                    .IsChecked(state, x => x.IsMirrorXPreset)
                                    .OnClick(_ => state.ApplyPreset(SymmetryPreset.MirrorX)),
                                new ToggleButton().Col(1)
                                    .Classes(PresetButtonClass)
                                    .Content("Y")
                                    .ToolTip_Tip(L("One horizontal axis"))
                                    .IsChecked(state, x => x.IsMirrorYPreset)
                                    .OnClick(_ => state.ApplyPreset(SymmetryPreset.MirrorY)),
                                new ToggleButton().Col(2)
                                    .Classes(PresetButtonClass)
                                    .Margin(0)
                                    .Content("X+Y")
                                    .ToolTip_Tip(L("Vertical and horizontal axes — four images"))
                                    .IsChecked(state, x => x.IsMirrorBothPreset)
                                    .OnClick(_ => state.ApplyPreset(SymmetryPreset.MirrorBoth))
                            ),

                        new SliderEx()
                            .Margin(0, 8, 0, 0)
                            .Label(L("Axes"))
                            .Minimum(SymmetrySettings.MinAxisCount)
                            .Maximum(SymmetrySettings.MaxAxisCount)
                            .Value(state, x => x.AxisCount, BindingMode.TwoWay),

                        new SliderEx()
                            .Margin(0, 8, 0, 0)
                            .Label(L("Angle"))
                            .Units("°")
                            .Minimum(0)
                            .Maximum(179)
                            .Value(state, x => x.Angle, BindingMode.TwoWay),

                        new Button()
                            .Margin(0, 12, 0, 0)
                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                            .HorizontalContentAlignment(HorizontalAlignment.Center)
                            .Content(L("Centre the axes"))
                            .IsEnabled(state, x => x.HasMovedCenter)
                            .OnClick(_ => state.ResetCenter()),

                        new TextBlock()
                            .Classes("caption")
                            .Margin(0, 8, 0, 0)
                            .TextWrapping(TextWrapping.Wrap)
                            .Text(L("Drag the grip at the end of an axis to move the axes; double-click it to centre them."))
                    )
            );

    public enum SymmetryPreset
    {
        MirrorX,
        MirrorY,
        MirrorBoth
    }

    public sealed partial class State : ObservableObject
    {
        private readonly SpriteEditorState _spriteEditorState;
        private readonly IDrawingService _drawingService;

        // The view writes state, the state watcher writes the view. Without this the sync pass would
        // re-enter the user-edit path and, worse, an in-flight preset would be half-applied (axis count
        // pushed, angle not yet) before the second property landed.
        private bool _syncing;

        [ObservableProperty]
        public partial bool IsEnabled { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMirrorXPreset))]
        [NotifyPropertyChangedFor(nameof(IsMirrorYPreset))]
        [NotifyPropertyChangedFor(nameof(IsMirrorBothPreset))]
        public partial double AxisCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMirrorXPreset))]
        [NotifyPropertyChangedFor(nameof(IsMirrorYPreset))]
        [NotifyPropertyChangedFor(nameof(IsMirrorBothPreset))]
        public partial double Angle { get; set; }

        [ObservableProperty]
        public partial bool HasMovedCenter { get; set; }

        public bool IsMirrorXPreset => IsPreset(1, 0);
        public bool IsMirrorYPreset => IsPreset(1, 90);
        public bool IsMirrorBothPreset => IsPreset(2, 0);

        public State(AppState appState, IDrawingService drawingService)
        {
            _spriteEditorState = appState.SpriteEditorState;
            _drawingService = drawingService;

            SyncFromState();
            _spriteEditorState.WatchFor(x => x.Symmetry, SyncFromState);
        }

        public void ApplyPreset(SymmetryPreset preset)
        {
            var center = _spriteEditorState.Symmetry.Center;
            _drawingService.SetSymmetry(preset switch
            {
                SymmetryPreset.MirrorY => SymmetrySettings.MirrorY(center),
                SymmetryPreset.MirrorBoth => SymmetrySettings.MirrorBoth(center),
                _ => SymmetrySettings.MirrorX(center)
            });
        }

        public void ResetCenter() => _drawingService.SetSymmetryCenter(null);

        private bool IsPreset(int axisCount, double angle) =>
            IsEnabled && (int)AxisCount == axisCount && Math.Abs(Angle - angle) < 0.5;

        private void SyncFromState()
        {
            var symmetry = _spriteEditorState.Symmetry;
            _syncing = true;
            IsEnabled = symmetry.IsEnabled;
            AxisCount = symmetry.AxisCount;
            Angle = symmetry.AngleDegrees;
            HasMovedCenter = symmetry.Center.HasValue;
            _syncing = false;
        }

        partial void OnIsEnabledChanged(bool value) => Push();

        partial void OnAxisCountChanged(double value) => Push();

        partial void OnAngleChanged(double value) => Push();

        private void Push()
        {
            if (_syncing) return;

            _drawingService.SetSymmetry(_spriteEditorState.Symmetry with
            {
                IsEnabled = IsEnabled,
                AxisCount = (int)Math.Round(AxisCount),
                AngleDegrees = (float)Angle
            });
        }
    }
}
