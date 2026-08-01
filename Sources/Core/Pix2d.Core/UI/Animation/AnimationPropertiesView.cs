using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Layout;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.Animation;

/// <summary>
/// Authoring surface for the animation metadata that the sprite-sheet exporter emits (roadmap H2.2
/// PR-3): the current frame's duration, the sprite's named animation tags, and the export pivot /
/// 9-slice anchors.
///
/// <para>Deliberately a popup rather than an in-timeline lane. The timeline is a horizontally
/// virtualised <c>ListBox</c> of 52 px tiles that already carries drag-reorder; a scroll-synced tag lane
/// over it is a project of its own and would be the first thing to break on a phone-portrait width.
/// Tag membership is instead surfaced *on* the tiles as a colored underline (see
/// <see cref="TimeLineView"/>), and everything editable lives here.</para>
/// </summary>
public partial class AnimationPropertiesView(AppState appState, IMessenger messenger)
    : ViewBase<AnimationPropertiesView.State>(new State(appState, messenger))
{
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<TextBox>(s => s.OfType<TextBox>().Class("meta-field"))
            .MinHeight(28)
            .Padding(6, 2)
            .FontSize(12),

        new Style<Button>(s => s.OfType<Button>().Class("meta-mini"))
            .MinWidth(0)
            .MinHeight(0)
            .Padding(6, 2)
            .FontSize(11)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
    ];

    protected override object Build(State state) =>
        new ScrollViewer()
            .MaxHeight(520)
            .Content(new StackPanel()
                .Margin(10, 4, 10, 12)
                .Children(
                    SectionLabel(L("Frame duration")),

                    // Shows the EFFECTIVE duration — an un-overridden frame reads the frame-rate default,
                    // so the field always answers "how long is this frame on screen".
                    new Grid().Cols("*,Auto").Margin(0, 0, 0, 2).Children(
                        new SliderEx()
                            .Label(L("Duration"))
                            .Units("ms")
                            .Minimum(10)
                            .Maximum(1000)
                            .Value(state, x => x.FrameDurationMs, BindingMode.TwoWay),
                        new Button()
                            .Col(1)
                            .Classes("meta-mini")
                            .Margin(6, 0, 0, 0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Content(L("Reset"))
                            .IsEnabled(state, x => x.HasFrameDurationOverride)
                            .ToolTip_Tip(L("Use the frame rate for this frame"))
                            .OnClick(_ => state.ResetFrameDuration())),

                    new TextBlock()
                        .Classes("caption")
                        .Margin(0, 0, 0, 10)
                        .Text(state, x => x.FrameDurationHint),

                    SectionLabel(L("Animation tags")),

                    new TextBlock()
                        .Classes("caption")
                        .Margin(0, 0, 0, 6)
                        .TextWrapping(TextWrapping.Wrap)
                        .IsVisible(state, x => x.HasNoTags)
                        .Text(L("Tags name a range of frames (idle, run, jump). They are exported as frameTags and can be exported one at a time.")),

                    new ItemsControl()
                        .ItemTemplate<TagRow, ItemsControl>(BuildTagRow)
                        .ItemsSource(state, x => x.Tags),

                    new Button()
                        .Classes("meta-mini")
                        .Margin(0, 6, 0, 12)
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .Content(L("+ Add tag"))
                        .IsEnabled(state, x => x.HasFrames)
                        .OnClick(_ => state.AddTag()),

                    SectionLabel(L("Export anchors")),

                    new TextBlock()
                        .Classes("caption")
                        .Margin(0, 0, 0, 6)
                        .TextWrapping(TextWrapping.Wrap)
                        .Text(L("Written to the sheet metadata as a slice; engines read them as the sprite origin and 9-slice borders.")),

                    LabeledPair(
                        L("Pivot X"), NumField().Value(state, x => x.PivotX, BindingMode.TwoWay),
                        L("Pivot Y"), NumField().Value(state, x => x.PivotY, BindingMode.TwoWay)),

                    new Button()
                        .Classes("meta-mini")
                        .Margin(0, 2, 0, 10)
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .Content(L("Clear pivot"))
                        .IsEnabled(state, x => x.HasPivot)
                        .OnClick(_ => state.ClearPivot()),

                    LabeledPair(
                        L("Slice L"), NumField().Value(state, x => x.SliceLeft, BindingMode.TwoWay),
                        L("Slice T"), NumField().Value(state, x => x.SliceTop, BindingMode.TwoWay)),

                    LabeledPair(
                        L("Slice R"), NumField().Value(state, x => x.SliceRight, BindingMode.TwoWay),
                        L("Slice B"), NumField().Value(state, x => x.SliceBottom, BindingMode.TwoWay)),

                    new Button()
                        .Classes("meta-mini")
                        .Margin(0, 2, 0, 0)
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .Content(L("Clear 9-slice"))
                        .IsEnabled(state, x => x.HasNineSlice)
                        .OnClick(_ => state.ClearNineSlice())));

    private static TextBlock SectionLabel(string text) =>
        new TextBlock()
            .Classes("body11")
            .Margin(0, 6, 0, 4)
            .Text(text.ToUpperInvariant());

    /// <summary>One tag: name on top, then From / To / direction / delete on a single wrapping row.</summary>
    private static Control BuildTagRow(TagRow row) =>
        new Border()
            .Margin(0, 0, 0, 6)
            .Padding(8, 6)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.ButtonBackgroundBrush)
            .Child(new StackPanel().Children(
                new Grid().Cols("Auto,*,Auto").Children(
                    // Index-derived swatch: the same color marks this tag's frames in the timeline.
                    new Border()
                        .Width(8).Height(18)
                        .CornerRadius(4)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .Background(new SolidColorBrush(row.Color)),

                    new TextBox()
                        .Col(1)
                        .Classes("meta-field")
                        .Margin(6, 0)
                        .Text(row, x => x.Name, BindingMode.TwoWay),

                    new Button()
                        .Col(2)
                        .Classes("meta-mini")
                        .Content("\xE711")
                        .FontFamily(StaticResources.Fonts.IconFontSegoe)
                        .ToolTip_Tip(L("Delete tag"))
                        .OnClick(_ => row.Delete())),

                new Grid().Cols("Auto,*,Auto,*,Auto").Margin(0, 4, 0, 0).Children(
                    new TextBlock().Classes("caption").VerticalAlignment(VerticalAlignment.Center).Text(L("From")),
                    new NumericUpDown()
                        .Col(1)
                        .Margin(4, 0, 8, 0)
                        .Minimum(0)
                        .Maximum(row.MaxFrameIndex)
                        .Increment(1)
                        .FormatString("0")
                        .ShowButtonSpinner(false)
                        .FontSize(12)
                        .Value(row, x => x.From, BindingMode.TwoWay),

                    new TextBlock().Col(2).Classes("caption").VerticalAlignment(VerticalAlignment.Center).Text(L("To")),
                    new NumericUpDown()
                        .Col(3)
                        .Margin(4, 0, 8, 0)
                        .Minimum(0)
                        .Maximum(row.MaxFrameIndex)
                        .Increment(1)
                        .FormatString("0")
                        .ShowButtonSpinner(false)
                        .FontSize(12)
                        .Value(row, x => x.To, BindingMode.TwoWay),

                    new ComboBox()
                        .Col(4)
                        .FontSize(11)
                        .ItemsSource(TagRow.Directions)
                        .SelectedItem(row, x => x.Direction, BindingMode.TwoWay))));

    /// <summary>A bare integer field — no spinner buttons, they cost more width than they are worth here.</summary>
    private static NumericUpDown NumField() =>
        new NumericUpDown()
            .Minimum(0)
            .Increment(1)
            .FormatString("0")
            .ShowButtonSpinner(false)
            .FontSize(12);

    /// <summary>Two labelled numeric fields on one row — the shape every anchor coordinate uses.</summary>
    private static Control LabeledPair(string firstLabel, Control first, string secondLabel, Control second) =>
        new Grid().Cols("Auto,*,Auto,*").Margin(0, 2).Children(
            new TextBlock().Classes("caption").VerticalAlignment(VerticalAlignment.Center).Text(firstLabel),
            first.Col(1).Margin(4, 0, 8, 0),
            new TextBlock().Col(2).Classes("caption").VerticalAlignment(VerticalAlignment.Center).Text(secondLabel),
            second.Col(3).Margin(4, 0, 0, 0));

    /// <summary>
    /// One editable tag. Edits are pushed straight to <see cref="SpriteEditor.UpdateAnimationTag"/>,
    /// which clamps and orders the range — so the two numeric fields can be edited independently
    /// without the view having to police <c>From &lt;= To</c> itself.
    /// </summary>
    public sealed partial class TagRow : ObservableObject
    {
        public static IReadOnlyList<string> Directions { get; } =
            [L("Forward"), L("Reverse"), L("Ping-pong"), L("Ping-pong reverse")];

        private readonly SpriteAnimationTag _tag;
        private readonly State _owner;
        private bool _isSyncing;

        [ObservableProperty] public partial string Name { get; set; } = "";
        [ObservableProperty] public partial decimal? From { get; set; }
        [ObservableProperty] public partial decimal? To { get; set; }
        [ObservableProperty] public partial string Direction { get; set; } = "";

        public int MaxFrameIndex { get; }
        public Color Color { get; }

        public TagRow(SpriteAnimationTag tag, State owner, int index, int maxFrameIndex)
        {
            _tag = tag;
            _owner = owner;
            MaxFrameIndex = Math.Max(0, maxFrameIndex);
            Color = GetTagColor(index);

            _isSyncing = true;
            Name = tag.Name;
            From = tag.From;
            To = tag.To;
            Direction = Directions[(int)tag.Direction];
            _isSyncing = false;
        }

        /// <summary>
        /// Distinct, evenly spaced hues so adjacent tags never read as the same band in the timeline.
        /// Derived from the index rather than persisted — a tag color is decoration, not document data.
        /// </summary>
        public static Color GetTagColor(int index)
        {
            var hue = (index * 47) % 360;
            return new HslColor(1, hue, 0.55, 0.62).ToRgb();
        }

        public void Delete() => _owner.RemoveTag(_tag);

        partial void OnNameChanged(string value) => Push();
        partial void OnFromChanged(decimal? value) => Push();
        partial void OnToChanged(decimal? value) => Push();
        partial void OnDirectionChanged(string value) => Push();

        private void Push()
        {
            if (_isSyncing)
                return;

            var direction = (SpriteAnimationDirection)Math.Max(0, Directions.ToList().IndexOf(Direction));
            _owner.UpdateTag(_tag, Name, (int)(From ?? 0), (int)(To ?? 0), direction);
        }
    }

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private bool _isSyncing;

        [ObservableProperty] public partial double FrameDurationMs { get; set; }
        [ObservableProperty] public partial bool HasFrameDurationOverride { get; set; }
        [ObservableProperty] public partial string FrameDurationHint { get; set; } = "";
        [ObservableProperty] public partial bool HasFrames { get; set; }
        [ObservableProperty] public partial bool HasNoTags { get; set; } = true;

        // decimal? matches NumericUpDown.Value, so these bind directly with no converter.
        [ObservableProperty] public partial decimal? PivotX { get; set; }
        [ObservableProperty] public partial decimal? PivotY { get; set; }
        [ObservableProperty] public partial bool HasPivot { get; set; }

        [ObservableProperty] public partial decimal? SliceLeft { get; set; }
        [ObservableProperty] public partial decimal? SliceTop { get; set; }
        [ObservableProperty] public partial decimal? SliceRight { get; set; }
        [ObservableProperty] public partial decimal? SliceBottom { get; set; }
        [ObservableProperty] public partial bool HasNineSlice { get; set; }

        public ObservableCollection<TagRow> Tags { get; } = [];

        public State(AppState appState, IMessenger messenger)
        {
            _appState = appState;

            Sync();

            _appState.SpriteEditorState.WatchFor(x => x.CurrentFrameIndex, Sync);
            _appState.SpriteEditorState.WatchFor(x => x.FramesCount, Sync);
            _appState.SpriteEditorState.WatchFor(x => x.FrameRate, Sync);

            // The edited sprite changes without a fresh load on a tab switch or an artboard activation.
            messenger.Register<ProjectLoadedMessage>(this, _ => Sync());
            messenger.Register<ProjectActivatedMessage>(this, _ => Sync());
            // Undo/redo of a metadata edit has to be reflected back into the fields.
            messenger.Register<OperationInvokedMessage>(this, _ => Sync());
        }

        private Pix2dSprite? Sprite => _appState.CurrentProject.CurrentEditedNode as Pix2dSprite;
        private SpriteEditor? Editor => _appState.CurrentProject.CurrentNodeEditor as SpriteEditor;

        private int CurrentFrame => _appState.SpriteEditorState.CurrentFrameIndex;

        public void ResetFrameDuration()
        {
            Editor?.SetFrameDuration(CurrentFrame, null);
            Sync();
        }

        public void AddTag()
        {
            Editor?.AddAnimationTag();
            Sync();
        }

        public void RemoveTag(SpriteAnimationTag tag)
        {
            Editor?.RemoveAnimationTag(tag);
            Sync();
        }

        public void UpdateTag(SpriteAnimationTag tag, string name, int from, int to, SpriteAnimationDirection direction)
        {
            Editor?.UpdateAnimationTag(tag, name, from, to, direction);
            // No Sync() here: it would rebuild the rows underneath the control the user is typing in.
            // The clamped values land back on the next Sync (frame change / undo / reopen).
        }

        public void ClearPivot()
        {
            Editor?.SetExportPivot(null);
            Sync();
        }

        public void ClearNineSlice()
        {
            Editor?.SetNineSlice(null);
            Sync();
        }

        partial void OnFrameDurationMsChanged(double value)
        {
            if (_isSyncing)
                return;

            Editor?.SetFrameDuration(CurrentFrame, (int)Math.Round(value));
            HasFrameDurationOverride = true;
            UpdateDurationHint();
        }

        partial void OnPivotXChanged(decimal? value) => PushPivot();
        partial void OnPivotYChanged(decimal? value) => PushPivot();

        partial void OnSliceLeftChanged(decimal? value) => PushNineSlice();
        partial void OnSliceTopChanged(decimal? value) => PushNineSlice();
        partial void OnSliceRightChanged(decimal? value) => PushNineSlice();
        partial void OnSliceBottomChanged(decimal? value) => PushNineSlice();

        private void PushPivot()
        {
            if (_isSyncing)
                return;

            Editor?.SetExportPivot(new SKPoint((float)(PivotX ?? 0), (float)(PivotY ?? 0)));
            HasPivot = true;
        }

        private void PushNineSlice()
        {
            if (_isSyncing)
                return;

            var margins = new NineSliceMargins
            {
                Left = (int)(SliceLeft ?? 0),
                Top = (int)(SliceTop ?? 0),
                Right = (int)(SliceRight ?? 0),
                Bottom = (int)(SliceBottom ?? 0)
            };

            // All-zero margins mean "no 9-slice" rather than a degenerate full-canvas centre rect.
            var isEmpty = margins is { Left: 0, Top: 0, Right: 0, Bottom: 0 };
            Editor?.SetNineSlice(isEmpty ? null : margins);
            HasNineSlice = !isEmpty;
        }

        private void Sync()
        {
            _isSyncing = true;

            var sprite = Sprite;
            var frameCount = sprite?.GetFramesCount() ?? 0;
            HasFrames = frameCount > 0;

            FrameDurationMs = sprite?.GetFrameDurationMs(CurrentFrame) ?? 0;
            HasFrameDurationOverride = sprite?.HasFrameDurationOverride(CurrentFrame) ?? false;
            UpdateDurationHint();

            Tags.Clear();
            var tags = sprite?.AnimationTags;
            if (tags != null)
            {
                for (var i = 0; i < tags.Count; i++)
                    Tags.Add(new TagRow(tags[i], this, i, frameCount - 1));
            }

            HasNoTags = Tags.Count == 0;

            var pivot = sprite?.ExportPivot;
            HasPivot = pivot != null;
            PivotX = (int)(pivot?.X ?? 0);
            PivotY = (int)(pivot?.Y ?? 0);

            var slice = sprite?.NineSlice;
            HasNineSlice = slice != null;
            SliceLeft = slice?.Left ?? 0;
            SliceTop = slice?.Top ?? 0;
            SliceRight = slice?.Right ?? 0;
            SliceBottom = slice?.Bottom ?? 0;

            _isSyncing = false;
        }

        private void UpdateDurationHint()
        {
            var sprite = Sprite;
            if (sprite == null)
            {
                FrameDurationHint = "";
                return;
            }

            FrameDurationHint = HasFrameDurationOverride
                ? string.Format(L("Frame {0} overrides the frame rate."), CurrentFrame)
                : string.Format(L("Default from {0} fps ({1} ms)."), (int)sprite.FrameRate, sprite.DefaultFrameDurationMs);
        }
    }
}
