using Pix2d.Command;
using Pix2d.Common.Behaviors;
using Pix2d.Common.Extensions;
using Pix2d.CommonNodes;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite.Operations;
using System.Collections.Specialized;
using System.Diagnostics;
using Pix2d.Abstract.Edit;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite.Operations.Layers;
using Pix2d.Operations.Effects;
using Pix2d.Plugins.Sprite.Operations.Effects;
using Pix2d.Abstract.Commands;

namespace Pix2d.UI.Layers;

public class LayersView : ComponentBase
{
    protected override object Build()
    {
        return new BlurPanel().Content(
            new Grid().Rows("36,*,62").Children(
                new Button()
                    .FontSize(20)
                    .Command(SpritePlugin.EditCommands.AddLayer)
                    .Content("\xE710")
                    .FontFamily(StaticResources.Fonts.IconFontSegoe),
                new ListBox()
                    .ScrollViewer_VerticalScrollBarVisibility(ScrollBarVisibility.Hidden)
                    .Styles(new Style<ListBoxItem>()
                        .Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(7))
                        .Setter(ListBoxItem.BorderThicknessProperty, new Thickness(1))
                        .Setter(ListBoxItem.ClipToBoundsProperty, true)
                    )
                    .Row(1).Margin(0).Padding(3)
                    .Background(Brushes.Transparent)
                    .BorderThickness(0)
                    .Classes("ItemsDragAndDrop")
                    .ItemsSource(Layers)
                    .SelectedIndex(() => SelectedIndex)
                    .ItemTemplate((LayerItemViewModel? itemVm) =>
                    {
                        if (itemVm == null)
                            return new TextBlock().Text("No layer");

                        return new LayerItemView(itemVm)
                            {
                                RightPointerPressed = () => ItemRightPointerPressed(itemVm),
                                LeftPointerPressed = () => ItemClicked(itemVm)
                            }
                            .AddBehavior(new ItemsListContextDragBehavior()
                                { Orientation = Orientation.Vertical });
                    }),
                new BackgroundSelectorView().Row(2)
            )
        );
    }

    private SpriteEditor? _editor;

    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    [Inject] private ICommandService CommandService { get; set; } = null!;

    private EditCommands EditCommands => CommandService.GetCommandList<EditCommands>()!;
    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;
    private ISpriteEditCommands SpriteEditCommands => CommandService.GetCommandList<ISpriteEditCommands>()!;

    private int SelectedIndex => ReverseIndex(AppState.SpriteEditorState.CurrentLayerIndex);

    public BulkAddObservableCollection<LayerItemViewModel> Layers { get; set; } = [];

    protected override void OnAfterInitialized()
    {
        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        Messenger.Register<SelectedFrameChangedMessage>(this, OnAnimationFrameChanged);

        AppState.CurrentProject.WatchFor(x => x.CurrentNodeEditor,
            () => OnEditorChanged(AppState.CurrentProject.CurrentNodeEditor));

        Layers.CollectionChanged += Layers_CollectionChanged;
    }

    private void OnAnimationFrameChanged(SelectedFrameChangedMessage obj)
    {
        if (!obj.IsPlaying)
        {
            InvalidateThumbnailItems();
        }
    }

    private int ReverseIndex(int index) => Layers.Count - index - 1;

    private bool _reorderingStarted;
    private ItemReorderInfo<LayerItemViewModel> _reorderInfo;

    private class ItemReorderInfo<TItem>
    {
        public TItem[] Items { get; set; }

        public int OldIndex { get; set; }

        public int NewIndex { get; set; }
    }

    private void Layers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            _reorderInfo = new ItemReorderInfo<LayerItemViewModel>()
            {
                Items = e.OldItems.OfType<LayerItemViewModel>().ToArray(),
                OldIndex = e.OldStartingIndex
            };
            _reorderingStarted = true;
        }

        if (e.Action == NotifyCollectionChangedAction.Add && _reorderingStarted)
        {
            _reorderInfo.NewIndex = e.NewStartingIndex;
            OnLayersReordered(_reorderInfo);

            _reorderingStarted = false;
            _reorderInfo = null;
        }
    }

    private void OnLayersReordered(ItemReorderInfo<LayerItemViewModel> reorderInfo)
    {
        var oldIndex = ReverseIndex(reorderInfo.OldIndex);
        var newIndex = ReverseIndex(reorderInfo.NewIndex);
        Debug.WriteLine($"Reordered layers from {oldIndex} to {newIndex}");
        _editor.ReorderLayers(oldIndex, newIndex);
    }

    private void OnOperationInvoked(OperationInvokedMessage operation)
    {
        if (operation.Operation is AddLayerOperation or DeleteLayerOperation or ReorderLayersOperation or MergeLayerOperation)
        {
            //drag and drop reorder - skip reloading
            if (_reorderingStarted) return;

            ReloadLayers();
            return;
        }

        if (operation.Operation is ISpriteEditorOperation spriteEditorOperation)
        {
            InvalidateThumbnailItems(spriteEditorOperation.AffectedLayerIndexes);
            UpdateSelectedLayerIndex();
        }
        else if (operation.Operation is AddEffectOperation or RemoveEffectOperation or BakeEffectOperation)
        {
            InvalidateThumbnailItems(operation.Operation.GetEditedNodes().OfType<Pix2dSprite.Layer>());
        }
        else if (operation.Operation is ChangeVisibilityOperationBase)
            InvalidateThumbnailItems(operation.Operation.GetEditedNodes().OfType<Pix2dSprite.Layer>());
    }

    private void InvalidateThumbnailItems()
    {
        foreach (var layer in Layers) //all layers
            layer?.Invalidate();
    }

    private void InvalidateThumbnailItems(IEnumerable<Pix2dSprite.Layer> layers)
    {
        foreach (var layer in layers)
            Layers.FirstOrDefault(x => x.SourceNode == layer)?.Invalidate();
    }

    private void InvalidateThumbnailItems(IEnumerable<int> affectedLayerIndexes)
    {
        foreach (var i in affectedLayerIndexes)
            Layers[ReverseIndex(i)]?.Invalidate();
    }


    private void UpdateSelectedLayerIndex()
    {
        AppState.SpriteEditorState.CurrentLayerIndex = _editor?.SelectedLayerIndex ?? 0;
    }

    private void OnEditorChanged(INodeEditor? editor)
    {
        _editor = editor as SpriteEditor;
        ReloadLayers();
    }

    private void ReloadLayers()
    {
        try
        {
            if (_editor == null)
                return;

            var layers = _editor.CurrentSprite.Layers.Reverse().Select(x => new LayerItemViewModel(x, _editor)
            {
                PreviewProvider = PreviewProvider
            }).ToList();

            Layers.Clear();
            Layers.AddRange(layers);
            UpdateSelectedLayerIndex();
        }
        finally
        {
            StateHasChanged();
        }
    }

    private SKBitmap PreviewProvider(LayerItemViewModel frameVm)
    {
        var sprite = _editor.CurrentSprite;
        var pw = 100;
        var bitmap = new SKBitmap(new SKImageInfo(pw, pw, Pix2DAppSettings.ColorType));
        var scale = 1f;
        var w = sprite.Size.Width;
        var h = sprite.Size.Height;
        scale = w > h ? pw / w : pw / h;
        frameVm.SourceNode.RenderCurrentFramePreview(bitmap, 1);
        return bitmap;
    }

    private void ItemClicked(LayerItemViewModel itemVm)
    {
        if (_editor.SelectedLayer == itemVm.SourceNode)
        {
            ViewCommands.ToggleLayerOptionsCommand.Execute();
        }

        _editor?.SelectLayer(itemVm.SourceNode);
        UpdateSelectedLayerIndex();
        StateHasChanged();
    }

    private void ItemRightPointerPressed(LayerItemViewModel itemVm)
    {
        ViewCommands.ToggleLayerOptionsCommand.Execute();
    }
}

public class LayerItemViewModel
{
    private readonly SpriteEditor _editor;
    private SKBitmap _preview;

    public SKBitmap Preview
    {
        get
        {
            _preview?.Dispose();
            _preview = PreviewProvider?.Invoke(this);
            return _preview;
        }
    }

    public LayerItemViewModel(Pix2dSprite.Layer sourceNode, SpriteEditor editor)
    {
        _editor = editor;
        SourceNode = sourceNode;
    }

    public Func<LayerItemViewModel, SKBitmap> PreviewProvider { get; set; }

    public Pix2dSprite.Layer SourceNode { get; set; }
    public bool IsSelected { get; set; }
    public Action? Invalidated { get; set; }

    public void Invalidate()
    {
        Invalidated?.Invoke();
    }

    public void ToggleLayerVisibility() => _editor?.ToggleLayerVisible(SourceNode);
}