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
    public TimeLineView(AppState appState, IMessenger messenger)
        : base(new State(appState, messenger))
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
                                : new Border()
                                    .Background(StaticResources.Brushes.CheckerTilesBrush)
                                    .CornerRadius(4)
                                    .ClipToBounds(true)
                                    .Child(
                                        new Rectangle()
                                            .Width(52)
                                            .Height(52)
                                            .Fill(itemVm, vm => vm.PreviewBrush))
                                    .AddBehavior(new ItemsListContextDragBehavior() { Orientation = Orientation.Horizontal })))
            ]);

    public sealed partial class State : ToolkitObservableObject
    {
        private readonly AppState _appState;
        private readonly IMessenger _messenger;
        private SpriteEditor? _editor;
        private bool _isSyncing;
        private ItemReorderInfo<AnimationFrameViewModel>? _reorderInfo;

        public State(AppState appState, IMessenger messenger)
        {
            _appState = appState;
            _messenger = messenger;

            _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
            _messenger.Register<SelectedFrameChangedMessage>(this, OnSelectedFrameChanged);
            _appState.CurrentProject.WatchFor(x => x.CurrentNodeEditor, () => OnEditorChanged(_appState.CurrentProject.CurrentNodeEditor));

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
                var frames = Enumerable
                    .Range(0, count)
                    .Select(_ => new AnimationFrameViewModel { PreviewProvider = PreviewProvider });

                Frames.ReloadItems(frames, silent: isReordering);
                _appState.SpriteEditorState.FramesCount = Frames.Count;
            }

            SyncSelectedIndex();
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
                || operation.Operation is ReorderAnimationFramesOperation)
            {
                ReloadFrames(_editor, operation.Operation is ReorderAnimationFramesOperation);
                return;
            }

            if (operation.Operation is ISpriteEditorOperation spriteEditorOperation)
            {
                foreach (var index in spriteEditorOperation.AffectedFrameIndexes)
                {
                    var frame = Frames[index];
                    frame?.Invalidate();
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
            _editor?.ReorderFrames(reorderInfo.OldIndex, reorderInfo.NewIndex);
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