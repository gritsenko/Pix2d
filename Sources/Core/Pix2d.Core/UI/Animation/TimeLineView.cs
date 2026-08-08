using Avalonia.Controls.Shapes;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Operations;
using Pix2d.Common.Behaviors;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.Primitives.SpriteEditor;
using Pix2d.UI.Resources;
using Pix2d.UI.Styles;
using SkiaSharp;
using System.Collections.Specialized;
using System.Diagnostics;
using ToolkitObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
using LegacyObservableObject = Mvvm.ObservableObject;

namespace Pix2d.UI.Animation;

public partial class TimeLineView : ViewBase<TimeLineView.State>
{
    public TimeLineView(AppState appState, IMessenger messenger, ICommandService commandService)
        : base(new State(appState, messenger, commandService))
    {
    }

    protected override StyleGroup BuildStyles() =>
    [
        new Style<Button>(s => s.Class("anim-btn"))
            .CornerRadius(10)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
            .FontSize(14)
            .Width(44)
            .Height(44)
            .Padding(0),

        // ToggleButton (the Play button) needs its own rule — AppStyles.cs has a global,
        // unscoped `Style<ToggleButton>()` (Width/Height 44) that otherwise wins over the
        // Narrow override below and leaves Play stuck at 44px while its sibling Buttons shrink.
        new Style<ToggleButton>(s => s.Class("anim-btn"))
            .CornerRadius(10)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
            .FontSize(14)
            .Width(44)
            .Height(44)
            .Padding(0),

        new Style<ListBoxItem>()
            .CornerRadius(4),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<Button>(s => s.Class("anim-btn"))
                .CornerRadius(10)
                .Foreground(StaticResources.Brushes.ForegroundBrush)
                .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                .FontSize(14)
                .Width(32)
                .Height(32)
                .Padding(0),

            // AppStyles.cs' global, unscoped `Style<ToggleButton>()` also sets Margin(6) — override it
            // here or Play keeps a visible gap around it that its sibling Buttons (Margin 0) don't have.
            new Style<ToggleButton>(s => s.Class("anim-btn"))
                .CornerRadius(10)
                .Foreground(StaticResources.Brushes.ForegroundBrush)
                .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                .FontSize(14)
                .Width(32)
                .Height(32)
                .Margin(0)
                .Padding(0),
        }
    ];

    protected override object Build(State state) =>
        new Grid()
            .Rows("56,*")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children([
                ViewFactory.Create<AnimationControlsView>(),

                new Rectangle().Row(1)
                    .Fill(StaticResources.Brushes.PanelsBackgroundBrush)
                    .RenderTransform(TransformOperations.Parse("translateY(60px)")),

                new ListBox().Row(1)
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .BorderThickness(0)
                    .ItemsPanel(new VirtualizingStackPanel().Orientation(Orientation.Horizontal))
                    .ItemsSource(state.Frames)
                    .SelectedIndex(state, x => x.SelectedIndex, BindingMode.TwoWay)
                    .ItemTemplate(
                        new FuncDataTemplate<AnimationFrameViewModel>((itemVm, _) =>
                            itemVm == null
                                ? new TextBlock().Text("No frame")
                                : itemVm.IsAddFrame
                                    ? new Border()
                                        .Width(52)
                                        .Height(52)
                                        .Background(StaticResources.Brushes.ButtonBackgroundBrush)
                                        .BorderBrush(Colors.White.WithAlpha(0.3f).ToBrush())
                                        .BorderThickness(1)
                                        .CornerRadius(4)
                                        .Child(
                                            new TextBlock()
                                                .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                                .FontSize(16)
                                                .Foreground(StaticResources.Brushes.IconForegroundBrush)
                                                .HorizontalAlignment(HorizontalAlignment.Center)
                                                .VerticalAlignment(VerticalAlignment.Center)
                                                .Text("\xe920"))
                                    : new Border()
                                        .Background(StaticResources.Brushes.CheckerTilesBrush)
                                        .CornerRadius(4)
                                        .ClipToBounds(true)
                                        .Child(
                                            // Z-stack: preview, then the animation-metadata markers on top.
                                            // Both are inside the clipped border so they follow the tile's
                                            // rounded corners and cost no extra layout width.
                                            new Panel()
                                                .Width(52)
                                                .Height(52)
                                                .Children(
                                                    new Rectangle()
                                                        .Width(52)
                                                        .Height(52)
                                                        .Fill(itemVm, vm => vm.PreviewBrush),

                                                    // Tag band along the bottom edge.
                                                    new Rectangle()
                                                        .Height(3)
                                                        .VerticalAlignment(VerticalAlignment.Bottom)
                                                        .IsVisible(itemVm, vm => vm.HasTag)
                                                        .Fill(itemVm, vm => vm.TagBrush),

                                                    // Corner dot: this frame has its own duration.
                                                    new Ellipse()
                                                        .Width(5)
                                                        .Height(5)
                                                        .Margin(0, 3, 3, 0)
                                                        .HorizontalAlignment(HorizontalAlignment.Right)
                                                        .VerticalAlignment(VerticalAlignment.Top)
                                                        .IsVisible(itemVm, vm => vm.HasDurationOverride)
                                                        .Fill(StaticResources.Brushes.ForegroundBrush),

                                                    // Link band along the TOP edge: this frame shares its
                                                    // image with others on the selected layer, so editing it
                                                    // edits them too. Deliberately a band rather than a chain
                                                    // glyph — the icon font (pix2d-icons-v3) is a curated set
                                                    // and has no link glyph, so a borrowed codepoint renders
                                                    // as nothing at all. It mirrors the tag band on the
                                                    // opposite edge, which is already the tile's vocabulary
                                                    // for "this frame belongs to a group".
                                                    new Rectangle()
                                                        .Height(3)
                                                        .VerticalAlignment(VerticalAlignment.Top)
                                                        .IsVisible(itemVm, vm => vm.IsLinked)
                                                        .Fill(StaticResources.Brushes.AccentButtonBrush)
                                                        .ToolTip_Tip(L("Linked frame — edits apply to every frame it is linked with"))))
                                        .AddBehavior(new ItemsListContextDragBehavior() { Orientation = Orientation.Horizontal })))
            ]);

    public sealed partial class State : ToolkitObservableObject
    {
        private readonly AppState _appState;
        private readonly IMessenger _messenger;
        private readonly Pix2dCommand _addFrameAtEndCommand;
        private SpriteEditor? _editor;
        private bool _isSyncing;
        private ItemReorderInfo<AnimationFrameViewModel>? _reorderInfo;

        public State(AppState appState, IMessenger messenger, ICommandService commandService)
        {
            _appState = appState;
            _messenger = messenger;
            _addFrameAtEndCommand = commandService.GetCommandList<ISpriteAnimationCommands>()!.AddFrameAtEnd;

            _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
            _messenger.Register<SelectedFrameChangedMessage>(this, OnSelectedFrameChanged);
            // The linked-cel marker reports the SELECTED layer's cel, so switching layers changes which
            // tiles show it even though no frame changed.
            _messenger.Register<SelectedLayerChangedMessage>(this, _ => ReloadFrames(_editor));
            _appState.WatchForCurrentProject(x => x.CurrentNodeEditor, () => OnEditorChanged(_appState.CurrentProject.CurrentNodeEditor));

            Frames.CollectionChanged += FramesCollectionChanged;
            OnEditorChanged(_appState.CurrentProject.CurrentNodeEditor);
        }

        [ObservableProperty]
        public partial int SelectedIndex { get; set; }

        public BulkAddObservableCollection<AnimationFrameViewModel> Frames { get; } = [];

        partial void OnSelectedIndexChanged(int value)
        {
            if (_isSyncing || value == -1)
                return;

            if (value < Frames.Count && Frames[value].IsAddFrame)
            {
                _addFrameAtEndCommand.Execute();
                return;
            }

            _appState.SpriteEditorState.CurrentFrameIndex = value;
            _editor?.SetFrameIndex(value);
        }

        private void OnSelectedFrameChanged(SelectedFrameChangedMessage message)
        {
            SyncSelectedIndex();
        }

        private void OnEditorChanged(INodeEditor? editor)
        {
            if (_editor != null)
                _editor.CurrentFrameChanged -= OnFrameChanged;

            _editor = editor as SpriteEditor;

            if (_editor != null)
                _editor.CurrentFrameChanged += OnFrameChanged;

            ReloadFrames(_editor);
        }

        private void ReloadFrames(SpriteEditor? editor, bool isReordering = false)
        {
            if (editor != null)
            {
                var count = _editor?.FramesCount ?? 1;
                var sprite = _appState.CurrentProject.CurrentEditedNode as Pix2dSprite;
                var frames = Enumerable
                    .Range(0, count)
                    .Select(i => new AnimationFrameViewModel
                    {
                        PreviewProvider = PreviewProvider,
                        TagColor = GetTagColor(sprite, i),
                        HasDurationOverride = sprite?.HasFrameDurationOverride(i) ?? false,
                        // Linking is per layer, so the marker reports the SELECTED layer's cel — the same
                        // layer the link/unlink commands act on.
                        IsLinked = sprite?.SelectedLayer?.IsFrameLinked(i) ?? false
                    })
                    .Append(new AnimationFrameViewModel { IsAddFrame = true });

                Frames.ReloadItems(frames, silent: isReordering);
                _appState.SpriteEditorState.FramesCount = count;
            }

            SyncSelectedIndex();
        }

        /// <summary>
        /// Colour of the first tag covering <paramref name="frameIndex"/>, transparent when none does.
        /// Ranges may overlap; the first match wins so the band stays stable while a tag is being
        /// widened over another. The palette is index-derived and shared with the tag editor.
        /// </summary>
        private static Color GetTagColor(Pix2dSprite? sprite, int frameIndex)
        {
            var tags = sprite?.AnimationTags;
            if (tags == null)
                return Colors.Transparent;

            for (var i = 0; i < tags.Count; i++)
            {
                if (tags[i].Covers(frameIndex))
                    return AnimationPropertiesView.TagRow.GetTagColor(i);
            }

            return Colors.Transparent;
        }

        private void SyncSelectedIndex()
        {
            _isSyncing = true;
            SelectedIndex = _editor?.CurrentFrameIndex ?? 0;
            _appState.SpriteEditorState.CurrentFrameIndex = SelectedIndex;
            _isSyncing = false;
        }

        private void OnFrameChanged(object? sender, SpriteFrameChangedEvenArgs e)
        {
            SyncSelectedIndex();
        }

        private void OnOperationInvoked(OperationInvokedMessage operation)
        {
            if (operation.Operation is AddAnimationFrameOperation
                || operation.Operation is DuplicateAnimationFrameOperation
                || operation.Operation is DeleteAnimationFrameOperation
                || operation.Operation is ReorderAnimationFramesOperation
                // Tag ranges and duration markers are rendered per tile, so a metadata edit (or its
                // undo) has to rebuild them too.
                || operation.Operation is EditAnimationMetaOperation
                // Same reason: linking changes the per-tile link marker on several frames at once, and
                // the fallback branch below only invalidates thumbnails — it never rebuilds the tile
                // view-models, so the marker would stay stale even though the pixels updated.
                || operation.Operation is LinkAnimationFramesOperation)
            {
                ReloadFrames(_editor, operation.Operation is ReorderAnimationFramesOperation);
                return;
            }

            if (operation.Operation is ISpriteEditorOperation spriteEditorOperation)
            {
                foreach (var index in spriteEditorOperation.AffectedFrameIndexes)
                {
                    // AffectedFrameIndexes are captured when the operation runs; by the time an undo/redo
                    // replays it the active sprite may have fewer frames (frames deleted, or a different
                    // artboard is now active and drives this Frames list), so the index can be stale.
                    if (index < 0 || index >= Frames.Count)
                        continue;

                    Frames[index]?.Invalidate();
                }
            }
        }

        private void FramesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                _reorderInfo = new ItemReorderInfo<AnimationFrameViewModel>
                {
                    OldIndex = e.OldStartingIndex,
                    NewIndex = e.NewStartingIndex
                };

                OnFramesReordered(_reorderInfo);
            }
        }

        private void OnFramesReordered(ItemReorderInfo<AnimationFrameViewModel> reorderInfo)
        {
            Debug.WriteLine($"Reordered frames from {reorderInfo.OldIndex} to {reorderInfo.NewIndex}");

            var frameCount = _editor?.FramesCount ?? 0;
            var oldIndex = reorderInfo.OldIndex;
            var newIndex = reorderInfo.NewIndex;

            // The Frames collection carries an extra "add frame" placeholder at index == frameCount.
            // The drag behavior treats it as an ordinary item, so a user can drag the placeholder itself
            // (oldIndex == frameCount) or drop a real frame onto its slot (newIndex == frameCount). Both
            // push an index past the real-frame range and throw inside ReorderAnimationFramesOperation.
            // Reject the placeholder as a drag source and clamp a drop-on-placeholder to the last frame.
            if (oldIndex < 0 || oldIndex >= frameCount)
            {
                ReloadFrames(_editor); // placeholder was dragged - rebuild the VM order the Move mangled
                return;
            }

            if (newIndex >= frameCount)
                newIndex = frameCount - 1;

            if (newIndex < 0 || newIndex == oldIndex)
            {
                ReloadFrames(_editor);
                return;
            }

            _editor?.ReorderFrames(oldIndex, newIndex);
        }

        private SKBitmap? PreviewProvider(AnimationFrameViewModel frameVm)
        {
            var index = Frames.IndexOf(frameVm);
            if (index < 0 || _editor == null)
                return null;

            var sprite = _editor.CurrentSprite;
            const int previewWidth = 48;
            var bitmap = new SKBitmap(new SKImageInfo(previewWidth, previewWidth, Pix2DAppSettings.ColorType));
            var width = sprite.Size.Width;
            var height = sprite.Size.Height;
            var scale = width > height ? previewWidth / width : previewWidth / height;
            sprite.RenderFramePreview(index, ref bitmap, scale, false);
            return bitmap;
        }
    }

    private sealed class ItemReorderInfo<TItem>
    {
        public int OldIndex { get; set; }

        public int NewIndex { get; set; }
    }
}

public class AnimationFrameViewModel : LegacyObservableObject
{
    private SKBitmap? _preview;

    public List<LayerFrameMeta> Layers
    {
        get => Get<List<LayerFrameMeta>>();
        set => Set(value);
    }

    public Func<AnimationFrameViewModel, SKBitmap?>? PreviewProvider { get; set; }
    public Action<AnimationFrameViewModel>? UpdatePropertiesAction { get; set; }

    public bool IsAddFrame
    {
        get => Get<bool>();
        set => Set(value);
    }

    /// <summary>
    /// Colour of the animation tag covering this frame, or transparent when none does. Drawn as a thin
    /// underline inside the tile — the read-only counterpart to the tag editor in
    /// <c>AnimationPropertiesView</c>, chosen over a scroll-synced tag lane above the virtualized list.
    /// </summary>
    public Color TagColor
    {
        get => Get<Color>();
        set => Set(value);
    }

    public bool HasTag => TagColor.A > 0;

    /// <summary>True when this frame overrides the sprite's frame rate with its own duration.</summary>
    public bool HasDurationOverride
    {
        get => Get<bool>();
        set => Set(value);
    }

    /// <summary>
    /// True when this frame is a linked cel on the selected layer — its image is shared with other frames,
    /// so an edit here changes all of them. Marked with a small chain glyph in the tile's top-left corner.
    /// </summary>
    public bool IsLinked
    {
        get => Get<bool>();
        set => Set(value);
    }

    public object TagBrush => new SolidColorBrush(TagColor);

    public SKBitmap? Preview
    {
        get
        {
            _preview?.Dispose();
            _preview = PreviewProvider?.Invoke(this);
            return _preview;
        }
    }

    public object PreviewBrush => Preview?.ToBrush() ?? StaticResources.Brushes.CheckerTilesBrush;

    public void Invalidate()
    {
        UpdatePropertiesAction?.Invoke(this);
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(PreviewBrush));
    }
}