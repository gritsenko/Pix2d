using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Operations;
using Pix2d.Command;
using Pix2d.Common.Behaviors;
using Pix2d.Common.Extensions;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Operations.Effects;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.Plugins.Sprite.Operations.Effects;
using Pix2d.Plugins.Sprite.Operations.Layers;
using Pix2d.Primitives;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;
using System.Collections.Specialized;
using System.Diagnostics;

namespace Pix2d.UI.Layers;

public partial class LayersView : ViewBase<LayersView.State>
{
    public LayersView(AppState appState, IMessenger messenger, ICommandService commandService, IServiceProvider serviceProvider)
        : base(new State(appState, messenger, commandService, serviceProvider))
    {
    }

    protected override object Build(State state) =>
        new BlurPanel().Content(
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
                        .Setter(ListBoxItem.ClipToBoundsProperty, true))
                    .Row(1).Margin(0).Padding(3)
                    .Background(Brushes.Transparent)
                    .BorderThickness(0)
                    .Classes("ItemsDragAndDrop")
                    .ItemsSource(state.Layers)
                    .SelectedIndex(state, x => x.SelectedIndex)
                    .ItemTemplate(
                        new FuncDataTemplate<LayerItemViewModel>((itemVm, _) =>
                        {
                            if (itemVm == null)
                                return new TextBlock().Text("No layer");

                            return state.CreateLayerItemView(itemVm);
                        }))
                    ,
                ViewFactory.Create<BackgroundSelectorView>().Row(2)
            )
        );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ViewCommands _viewCommands;
        private readonly IMessenger _messenger;
        private readonly IServiceProvider _serviceProvider;
        private SpriteEditor? _editor;
        private ItemReorderInfo<LayerItemViewModel>? _reorderInfo;

        public State(AppState appState, IMessenger messenger, ICommandService commandService, IServiceProvider serviceProvider)
        {
            _appState = appState;
            _messenger = messenger;
            _serviceProvider = serviceProvider;
            _viewCommands = commandService.GetCommandList<ViewCommands>()!;

            _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
            _messenger.Register<SelectedFrameChangedMessage>(this, OnAnimationFrameChanged);

            _appState.CurrentProject.WatchFor(x => x.CurrentNodeEditor, () => OnEditorChanged(_appState.CurrentProject.CurrentNodeEditor));

            Layers.CollectionChanged += LayersCollectionChanged;
            OnEditorChanged(_appState.CurrentProject.CurrentNodeEditor);
        }

        [ObservableProperty]
        public partial int SelectedIndex { get; set; } = -1;

        public BulkAddObservableCollection<LayerItemViewModel> Layers { get; } = [];

        public Control CreateLayerItemView(LayerItemViewModel itemVm)
        {
            var itemView = ActivatorUtilities.CreateInstance<LayerItemView>(_serviceProvider, itemVm);
            itemView.RightPointerPressed = () => ItemRightPointerPressed(itemVm);
            itemView.LeftPointerPressed = () => ItemClicked(itemVm);
            return itemView.AddBehavior(new ItemsListContextDragBehavior() { Orientation = Orientation.Vertical });
        }

        private int ReverseIndex(int index) => Layers.Count - index - 1;

        private void OnAnimationFrameChanged(SelectedFrameChangedMessage message)
        {
            if (!message.IsPlaying)
                InvalidateThumbnailItems();
        }

        private void LayersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                _reorderInfo = new ItemReorderInfo<LayerItemViewModel>
                {
                    OldIndex = e.OldStartingIndex,
                    NewIndex = e.NewStartingIndex
                };

                OnLayersReordered(_reorderInfo);
            }
        }

        private void OnLayersReordered(ItemReorderInfo<LayerItemViewModel> reorderInfo)
        {
            var oldIndex = ReverseIndex(reorderInfo.OldIndex);
            var newIndex = ReverseIndex(reorderInfo.NewIndex);
            Debug.WriteLine($"Reordered layers from {oldIndex} to {newIndex}");
            _editor?.ReorderLayers(oldIndex, newIndex);
        }

        private void OnOperationInvoked(OperationInvokedMessage operation)
        {
            if (operation.Operation is AddLayerOperation or DeleteLayerOperation or ReorderLayersOperation or MergeLayerOperation)
            {
                ReloadLayers(operation.Operation is ReorderLayersOperation);
            }
            else if (operation.Operation is ISpriteEditorOperation spriteEditorOperation)
            {
                InvalidateThumbnailItems(spriteEditorOperation.AffectedLayerIndexes);
                UpdateSelectedLayerIndex();
            }
            else if (operation.Operation is AddEffectOperation or RemoveEffectOperation or BakeEffectOperation)
            {
                InvalidateThumbnailItems(operation.Operation.GetEditedNodes().OfType<Pix2dSprite.Layer>());
            }
            else if (operation.Operation is ChangeVisibilityOperationBase)
            {
                InvalidateThumbnailItems(operation.Operation.GetEditedNodes().OfType<Pix2dSprite.Layer>());
            }
        }

        private void InvalidateThumbnailItems()
        {
            foreach (var layer in Layers)
                layer?.Invalidate();
        }

        private void InvalidateThumbnailItems(IEnumerable<Pix2dSprite.Layer> layers)
        {
            foreach (var layer in layers)
                Layers.FirstOrDefault(x => x.SourceNode == layer)?.Invalidate();
        }

        private void InvalidateThumbnailItems(IEnumerable<int> affectedLayerIndexes)
        {
            foreach (var index in affectedLayerIndexes)
                Layers[ReverseIndex(index)]?.Invalidate();
        }

        private void UpdateSelectedLayerIndex()
        {
            _appState.SpriteEditorState.CurrentLayerIndex = _editor?.SelectedLayerIndex ?? 0;
            SelectedIndex = Layers.Count == 0 ? -1 : ReverseIndex(_appState.SpriteEditorState.CurrentLayerIndex);
        }

        private void OnEditorChanged(INodeEditor? editor)
        {
            _editor = editor as SpriteEditor;
            ReloadLayers();
        }

        private void ReloadLayers(bool isReordering = false)
        {
            if (_editor == null)
                return;

            var layers = _editor.CurrentSprite.Layers.Reverse().Select(layer => new LayerItemViewModel(layer, _editor)
            {
                PreviewProvider = PreviewProvider
            }).ToList();

            Layers.ReloadItems(layers, silent: isReordering);
            UpdateSelectedLayerIndex();
        }

        private SKBitmap? PreviewProvider(LayerItemViewModel frameVm)
        {
            if (_editor == null)
                return null;

            const int previewWidth = 100;
            var bitmap = new SKBitmap(new SKImageInfo(previewWidth, previewWidth, Pix2DAppSettings.ColorType));
            frameVm.SourceNode.RenderCurrentFramePreview(bitmap, 1);
            return bitmap;
        }

        public void ItemClicked(LayerItemViewModel itemVm)
        {
            var oldSelectedLayer = _editor?.SelectedLayer;
            if (oldSelectedLayer == itemVm.SourceNode)
            {
                _viewCommands.ToggleLayerOptionsCommand.Execute();
            }

            _editor?.SelectLayer(itemVm.SourceNode);

            if (oldSelectedLayer != null && oldSelectedLayer != itemVm.SourceNode)
                Layers.FirstOrDefault(x => x.SourceNode == oldSelectedLayer)?.Invalidate();

            itemVm.Invalidate();
            UpdateSelectedLayerIndex();
        }

        public void ItemRightPointerPressed(LayerItemViewModel itemVm)
        {
            _viewCommands.ToggleLayerOptionsCommand.Execute();
        }
    }

    private sealed class ItemReorderInfo<TItem>
    {
        public int OldIndex { get; set; }

        public int NewIndex { get; set; }
    }
}

public class LayerItemViewModel
{
    private readonly SpriteEditor _editor;
    private SKBitmap? _preview;

    public SKBitmap? Preview
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

    public Func<LayerItemViewModel, SKBitmap?>? PreviewProvider { get; set; }

    public Pix2dSprite.Layer SourceNode { get; set; }

    public bool IsSelected => _editor.SelectedLayer == SourceNode;

    public Action? Invalidated { get; set; }

    public void Invalidate()
    {
        Invalidated?.Invoke();
    }

    public void ToggleLayerVisibility() => _editor.ToggleLayerVisible(SourceNode);
}