using Avalonia.Controls.Shapes;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Pix2d.Common.Behaviors;
using Pix2d.Common.Extensions;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using SkiaSharp;
using System.Collections.Specialized;
using System.Diagnostics;
using Pix2d.Abstract.Operations;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.Primitives.SpriteEditor;
using Pix2d.UI.Shared;
using Mvvm;
using Pix2d.Abstract.Edit;
using Pix2d.CommonNodes;

namespace Pix2d.UI.Animation;

public class TimeLineView : LocalizedComponentBase
{

    protected override StyleGroup BuildStyles() =>
    [
        new Style<Button>(s => s.Class("anim-btn"))
            .CornerRadius(10)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
            .FontSize(14)
            .Width(44)
            .Height(44)
            .Padding(0)
    ];

    protected override object Build() =>
        new Grid()
            .Rows("56,*")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children([
                new AnimationControlsView(),

                new Rectangle().Row(1)
                    .Fill(StaticResources.Brushes.PanelsBackgroundBrush)
                    .RenderTransform(TransformOperations.Parse("translateY(60px)")),

                new ListBox().Row(1)
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .BorderThickness(0)
                    .ItemsPanel(new VirtualizingStackPanel().Orientation(Orientation.Horizontal))
                    .ItemsSource(Frames)
                    .SelectedIndex(() => AppState.SpriteEditorState.CurrentFrameIndex, v =>
                    {
                        if (v == -1)
                            return;

                        AppState.SpriteEditorState.CurrentFrameIndex = v;
                        _editor?.SetFrameIndex(v);
                    })
                    .ItemTemplate<AnimationFrameViewModel>(itemVm =>
                        new Border()
                            .Background(StaticResources.Brushes.CheckerTilesBrush)
                            .Child(
                                new Rectangle()
                                    .Width(52)
                                    .Height(52)
                                    .Fill(itemVm?.Preview, bindingMode: BindingMode.OneWay, bindingSource:itemVm,
                                        converter: StaticResources.Converters.SKBitmapToIBrushConverter)
                            ).AddBehavior(new ItemsListContextDragBehavior() { Orientation = Orientation.Horizontal })
                    ) //ItemTemplate
            ]);


    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private AppState AppState { get; set; } = null!;

    private SpriteEditor? _editor;
    private bool _reorderingStarted;

    public BulkAddObservableCollection<AnimationFrameViewModel> Frames { get; set; } = [];

    protected override void OnAfterInitialized()
    {
        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        AppState.CurrentProject.WatchFor(x => x.CurrentNodeEditor, () => OnEditorChanged(AppState.CurrentProject.CurrentNodeEditor));

        Frames.CollectionChanged += Frames_CollectionChanged;
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

    private void ReloadFrames(SpriteEditor? editor)
    {
        Debug.WriteLine($"Reloading frames:{editor}");
        Frames.Clear();

        if (editor == null)
        {
            StateHasChanged();
            return;
        }

        var cnt = _editor?.FramesCount ?? 1;
        
        var frames = Enumerable
            .Range(0, cnt)
            .Select(_ => new AnimationFrameViewModel { PreviewProvider = PreviewProvider });
        Frames.AddRange(frames);

        AppState.SpriteEditorState.CurrentFrameIndex = _editor?.CurrentFrameIndex ?? 0;
        AppState.SpriteEditorState.FramesCount = Frames.Count;

        StateHasChanged();
    }

    private void OnFrameChanged(object? sender, SpriteFrameChangedEvenArgs e)
    {
        AppState.SpriteEditorState.CurrentFrameIndex = _editor.CurrentFrameIndex;
        StateHasChanged();
    }

    private void OnOperationInvoked(OperationInvokedMessage operation)
    {
        if (operation.Operation is AddAnimationFrameOperation
            || operation.Operation is DuplicateAnimationFrameOperation
            || operation.Operation is DeleteAnimationFrameOperation)
        {
            try
            {
                ReloadFrames(_editor);
                return;
            }
            finally
            {
                StateHasChanged();
            }
        }

        if (operation.Operation is ISpriteEditorOperation spriteEditorOperation)
        {
            foreach (var i in spriteEditorOperation.AffectedFrameIndexes)
            {
                var frame = Frames[i];
                frame?.Invalidate();
            }
        }
    }

    private ItemReorderInfo<AnimationFrameViewModel> _reorderInfo;
    private void Frames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            _reorderInfo = new ItemReorderInfo<AnimationFrameViewModel>()
            {
                Items = e.OldItems.OfType<AnimationFrameViewModel>().ToArray(),
                OldIndex = e.OldStartingIndex
            };
            _reorderingStarted = true;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && _reorderingStarted)
        {
            _reorderInfo.NewIndex = e.NewStartingIndex;
            OnFramesReordered(_reorderInfo);

            _reorderingStarted = false;
            _reorderInfo = null;
        }
    }
    private void OnFramesReordered(ItemReorderInfo<AnimationFrameViewModel> reorderInfo)
    {
        Debug.WriteLine($"Reordered frames from {reorderInfo.OldIndex} to {reorderInfo.NewIndex}");
        _editor.ReorderFrames(reorderInfo.OldIndex, reorderInfo.NewIndex);
    }

    private SKBitmap PreviewProvider(AnimationFrameViewModel frameVm)
    {
        var index = Frames.IndexOf(frameVm);
        if (index < 0)
        {
            return null;
        }
        var sprite = _editor.CurrentSprite;
        var pw = 48;
        var bitmap = new SKBitmap(new SKImageInfo(pw, pw, Pix2DAppSettings.ColorType));
        var scale = 1f;
        var w = sprite.Size.Width;
        var h = sprite.Size.Height;
        scale = w > h ? pw / w : pw / h;
        sprite.RenderFramePreview(index, ref bitmap, scale, false);
        return bitmap;
    }

    private class ItemReorderInfo<TItem>
    {
        public TItem[] Items { get; set; }

        public int OldIndex { get; set; }

        public int NewIndex { get; set; }
    }
}

public class AnimationFrameViewModel : ObservableObject
{
    private SKBitmap _preview;

    public List<LayerFrameMeta> Layers
    {
        get => Get<List<LayerFrameMeta>>();
        set => Set(value);
    }

    public Func<AnimationFrameViewModel, SKBitmap> PreviewProvider { get; set; }
    public Action<AnimationFrameViewModel> UpdatePropertiesAction { get; set; }

    public SKBitmap Preview
    {
        get
        {
            _preview?.Dispose();
            _preview = PreviewProvider?.Invoke(this);
            return _preview;
        }
    }

    public void Invalidate()
    {
        UpdatePropertiesAction?.Invoke(this);
        OnPropertyChanged(nameof(Layers));
        OnPropertyChanged(nameof(Preview));
    }
}