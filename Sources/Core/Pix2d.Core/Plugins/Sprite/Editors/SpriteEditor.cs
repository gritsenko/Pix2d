using System.Diagnostics;
using Avalonia.Threading;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Import;
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Modules.Sprite.Editors;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.Plugins.Sprite.Operations.Layers;
using Pix2d.Primitives.Edit;
using Pix2d.Primitives.SpriteEditor;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Editors;

public class SpriteEditor : ISpriteEditor, IImportTarget
{
    /// <summary>
    /// Keep operation that will be invoked by timeout, like change opacity, or some effects changes (usually with sliders in UI)
    /// </summary>
    private EditOperationBase? _debouncedOperation;

    private readonly Lock _updateOperationLock = new();

    private readonly Timer _operationTimer;

    public event EventHandler PlaybackStateChanged;

    public event EventHandler LayersChanged;
    public event EventHandler SelectedLayerChanged;

    public event EventHandler<FramesChangedEventArgs> FramesChanged;
    public event EventHandler<SpriteFrameChangedEvenArgs> CurrentFrameChanged;


    private readonly IDrawingService _drawingService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IMessenger _messenger;
    private readonly IOperationService _operationService;

    private readonly Timer _timer;
    private readonly SpriteEditorState _editorState;

    public Pix2dSprite.Layer SelectedLayer => CurrentSprite?.SelectedLayer;

    public int SelectedLayerIndex => CurrentSprite.SelectedLayerIndex;

    public Pix2dSprite CurrentSprite { get; private set; }

    public SpriteEditor(IDrawingService drawingService, IViewPortRefreshService viewPortRefreshService, IMessenger messenger, AppState state, IOperationService operationService)
    {
        _drawingService = drawingService;
        _viewPortRefreshService = viewPortRefreshService;
        _messenger = messenger;
        _operationService = operationService;

        _messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        _messenger.Register<ProjectCloseMessage>(this, OnProjectClose);


        _timer = new Timer(OnTick, this, -1, -1);
        _editorState = state.SpriteEditorState;

        _editorState.WatchFor(x => x.ShowOnionSkin, () =>
        {
            CurrentSprite.OnionSkinSettings.IsEnabled = _editorState.ShowOnionSkin;
            _viewPortRefreshService.Refresh();
        });

        _editorState.WatchFor(x => x.FrameRate, () =>
        {
            CurrentSprite.FrameRate = _editorState.FrameRate;

            if(IsPlaying)
                _timer.Change(1000 / FrameRate, 1000 / FrameRate);

            _viewPortRefreshService.Refresh();
        });

        _operationTimer = new Timer(OnOperationTimerTick, this, -1, -1);
    }

    private void OnOperationTimerTick(object state)
    {
        PerformPendingOperation();
    }

    private void OnProjectClose(ProjectCloseMessage obj)
    {
        Stop();
    }

    private void OnOperationInvoked(OperationInvokedMessage e)
    {
        if (e.OperationType != OperationEventType.Perform)
        {
            if (e.Operation is AddAnimationFrameOperation add)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Delete
                    : FramesChangedType.Add;
                OnFramesChanged(changeType, [add.FrameIndex]);
            }
            else if (e.Operation is DeleteAnimationFrameOperation del)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Add
                    : FramesChangedType.Delete;
                OnFramesChanged(changeType, [del.FrameIndex]);
            }
            else if (e.Operation is DuplicateAnimationFrameOperation per)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Delete
                    : FramesChangedType.Add;
                OnFramesChanged(changeType, [per.FrameIndex]);
                OnFramesChanged(FramesChangedType.Reset, null);
            }
        }

        if (e.Operation.AffectsNodeStructure || e.Operation is ResizeSpriteOperationBase)
        {
            OnLayersChanged();
            _drawingService.UpdateDrawingTarget();
        }
    }

    private void OnTick(object state)
    {
        //todo: exceptions on app closing
        if (FrameRate == 0)
        {
            // stop playing on change frame rate to zero
            Dispatcher.UIThread.Invoke(TogglePlay);
            return;
        }

        // Changing frames modifies the node structure. If this is done not in the UI thread, it can result
        // in race conditions with processing user input.
        Dispatcher.UIThread.Invoke(() => SetFrameIndex(CurrentSprite.NextFrameIndex));
    }


    public void SetTargetNode(SKNode node)
    {
        var oldSprite = CurrentSprite;

        oldSprite?.SetEditMode(false);

        CurrentSprite = node as Pix2dSprite;

        CurrentSprite?.SetEditMode(true);

        if (oldSprite != CurrentSprite)
        {
            OnFramesChanged(FramesChangedType.Reset, null);
        }
        _drawingService.UpdateDrawingTarget();
    }


    #region layers

    public void ToggleLayerVisible(Pix2dSprite.Layer layer)
    {
        var operation = new ChangeVisibilityOperationBase([layer]);
        layer.IsVisible = !layer.IsVisible;
        operation.SetFinalData();
        _operationService.PushOperations(operation);
        _viewPortRefreshService.Refresh();
    }


    public Pix2dSprite.Layer AddEmptyLayer(Pix2dSprite.Layer addAfter = null)
    {
        var oldSelectedLayer = SelectedLayer;
        var newLayer = CurrentSprite.AddLayer();

        var addLayerOperation = new AddLayerOperation(newLayer.Yield(), oldSelectedLayer);
        _operationService.PushOperations(addLayerOperation);
        OnLayersChanged();

        _drawingService.UpdateDrawingTarget();

        return newLayer;
    }

    public void DeleteLayer(Pix2dSprite.Layer layerToDelete = null)
    {
        var layer = layerToDelete ?? SelectedLayer;
        var operation = new DeleteLayerOperation(layer.Yield());
        _operationService.InvokeAndPushOperations(operation);
        //CurrentSprite.DeleteLayer(layer);
        //operation.PushToHistory();
        OnLayersChanged();
    }

    public void DuplicateLayer(Pix2dSprite.Layer layer = null, int insertIndex = -1)
    {
        var oldSelectedLayer = SelectedLayer;
        var newLayer = CurrentSprite.DuplicateLayer(layer ?? SelectedLayer, insertIndex);
        var operation = new DuplicateLayerOperation(newLayer.Yield(), oldSelectedLayer);
        _operationService.PushOperations(operation);
        OnLayersChanged();
    }

    public void MergeDownLayer(Pix2dSprite.Layer layer = null)
    {
        if (!CanMergeDownLayer(layer ?? SelectedLayer))
            return;

        var operation = new MergeLayerOperation((layer ?? SelectedLayer).Yield());
        _operationService.InvokeAndPushOperations(operation);

        OnLayersChanged();
    }

    public void SetOpacity(float newOpacity)
    {
        var operation = new ChangeOpacityOperation([SelectedLayer]);
        SelectedLayer.Opacity = newOpacity;
        operation.SetFinalData();

        SetDebouncedOperation(operation);
    }

    private void SetDebouncedOperation<TOperation>(TOperation operation) where TOperation : EditOperationBase
    {
        lock (_updateOperationLock)
        {
            if (_debouncedOperation is not TOperation)
            {
                PerformPendingOperation();
            }

            _debouncedOperation = operation;

            _operationTimer.Change(1000, 0);
        }
    }

    private void PerformPendingOperation()
    {
        _operationTimer.Change(-1, -1);
        if (_debouncedOperation != null)
            _operationService.PushOperations(_debouncedOperation);
    }

    public bool CanMergeDownLayer(Pix2dSprite.Layer layer = null)
    {
        return CurrentSprite?.CanMergeDownLayer(layer ?? SelectedLayer) ?? false;
    }

    public void Rotate(float angle, Pix2dSprite.Layer layer = null)
    {
        if (Math.Abs(angle - 90) < 0.1)
        {
            var selectionEditor = _drawingService.GetSelectionEditor();
            if (selectionEditor.HasSelection)
            {
                selectionEditor.RotateSelection(90);
            }
            else
            {
                RotateCurrentFrame(CurrentSprite);
                _drawingService.UpdateDrawingTarget();
            }

            _viewPortRefreshService.Refresh();
        }
    }

    public void RotateSprite()
    {
        var operation = new EditSpriteOperation(CurrentSprite) { Callback = OnLayersChanged };
        var rotatedNodes = new HashSet<SpriteNode>();
        foreach (var layer in CurrentSprite.Layers)
        {
            for (var i = 0; i < CurrentSprite.GetFramesCount(); i++)
            {
                var node = layer.GetSpriteByFrame(i);
                if (node != null && !rotatedNodes.Contains(node))
                {
                    node.RotateSourceBitmap(true);
                    rotatedNodes.Add(node);
                }

            }

            layer.Size = new SKSize(layer.Size.Height, layer.Size.Width);
        }

        CurrentSprite.Size = new SKSize(CurrentSprite.Size.Height, CurrentSprite.Size.Width);
        operation.SetFinalData();
        _operationService.PushOperations(operation);

        _viewPortRefreshService?.Refresh();
        _drawingService.UpdateDrawingTarget();
        OnLayersChanged();
    }

    public void RotateCurrentFrame()
    {
        var operations = new List<IEditOperation>();
        if (Math.Abs(CurrentSprite.Size.Width - CurrentSprite.Size.Height) > 0.1)
        {
            var size = Math.Max(CurrentSprite.Size.Width, CurrentSprite.Size.Height);
            var resizeOperation = new ResizeSpriteOperationBase(CurrentSprite, new SKSize(size, size))
            {
                VerticalAnchor = 0.5f,
                HorizontalAnchor = 0.5f
            };
            resizeOperation.OnPerform();
            operations.Add(resizeOperation);
        }

        var rotateOperation = new EditFrameOperation(CurrentSprite);
        RotateCurrentFrame(CurrentSprite);
        rotateOperation.SetFinalData();

        operations.Add(rotateOperation);
        _operationService.PushOperations(operations.ToArray());
    }

    private void RotateCurrentFrame(Pix2dSprite sprite)
    {
        Debug.Assert(Math.Abs(sprite.Size.Width - sprite.Size.Height) < 0.1);

        foreach (var layer in sprite.Layers)
        {
            layer.RotateSourceBitmap(sprite.CurrentFrameIndex, true);
        }

        _viewPortRefreshService?.Refresh();
        _drawingService.UpdateDrawingTarget();
        OnLayersChanged();
    }

    public void Flip(FlipMode mode, Pix2dSprite.Layer layer = null)
    {
        var selectionEditor = _drawingService.GetSelectionEditor();
        if (selectionEditor.HasSelection)
        {
            selectionEditor.FlipSelection(mode);
        }
        else
        {
            FlipLayer(layer ?? SelectedLayer, mode);
            _drawingService.UpdateDrawingTarget();
        }

        _viewPortRefreshService.Refresh();
    }

    public void FlipLayer(Pix2dSprite.Layer layer, FlipMode mode)
    {
        if (!(layer.Nodes[CurrentFrameIndex] is BitmapNode sprite))
            return;

        switch (mode)
        {
            case FlipMode.Horizontal:
                sprite.FlipHorizontal();
                break;
            case FlipMode.Vertical:
                sprite.FlipVertical();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    public void SendLayerBackward(Pix2dSprite.Layer layer = null)
    {
        var targetLayer = layer ?? SelectedLayer;
        if (targetLayer.Index - 1 < 0)
            return;

        ReorderLayers(targetLayer.Index, targetLayer.Index - 1);
    }

    public void BringLayerForward(Pix2dSprite.Layer layer = null)
    {
        var targetLayer = layer ?? SelectedLayer;

        if (targetLayer.Index + 1 >= targetLayer.Parent.Nodes.Count)
            return;

        ReorderLayers(targetLayer.Index, targetLayer.Index + 1);
    }
    public void ReorderLayers(int oldIndex, int newIndex)
    {
        var operation = new ReorderLayersOperation(CurrentSprite, oldIndex, newIndex);
        _operationService.InvokeAndPushOperations(operation);

        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService?.Refresh();
        OnLayersChanged();
    }

    public void SelectLayer(Pix2dSprite.Layer layer)
    {
        CurrentSprite.SelectLayer(layer);
        OnSelectedLayerChanged();
    }

    protected virtual void OnLayersChanged()
    {
        LayersChanged?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSelectedLayerChanged()
    {
        _drawingService.UpdateDrawingTarget();
        SelectedLayerChanged?.Invoke(this, EventArgs.Empty);
        _viewPortRefreshService.Refresh();
    }


    #endregion

    #region animation


    public bool IsPlaying
    {
        get => _editorState.IsPlayingAnimation;
        private set => _editorState.IsPlayingAnimation = value;
    }

    public int CurrentFrameIndex => CurrentSprite.CurrentFrameIndex;

    public int FramesCount => GetFramesCount();

    public int FrameRate
    {
        get => (int)CurrentSprite.FrameRate;
        set => CurrentSprite.FrameRate = value;
    }

    public void SetFrameIndex(int currentFrame)
    {
        if (CurrentSprite == null)
            return;

        _drawingService.SplitCurrentOperation();
        CurrentSprite.SetFrameIndex(currentFrame);
        _drawingService.UpdateDrawingTarget();
        OnCurrentFrameChanged();
    }

    private int GetFramesCount()
    {
        return CurrentSprite?.GetFramesCount() ?? 0;
    }

    public void AddFrame()
    {
        var operation = new AddAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        _operationService.InvokeAndPushOperations(operation);
        OnFramesChanged(FramesChangedType.Add, [CurrentFrameIndex]);
    }
    public void DuplicateFrame()
    {
        var operation = new DuplicateAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        _operationService.InvokeAndPushOperations(operation);
        OnFramesChanged(FramesChangedType.Add, [CurrentFrameIndex]);
    }

    public void DeleteFrame(int index = -1)
    {
        if (CurrentSprite.GetFramesCount() <= 1)
        {
            return;
        }

        if (index == -1)
        {
            index = CurrentFrameIndex;
        }

        var operation = new DeleteAnimationFrameOperation(CurrentSprite, index);
        _operationService.InvokeAndPushOperations(operation);

        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService?.Refresh();
        OnFramesChanged(FramesChangedType.Delete, [index]);
    }

    public void ReorderFrames(int oldIndex, int newIndex)
    {
        var operation = new ReorderAnimationFramesOperation(CurrentSprite, oldIndex, newIndex);
        _operationService.InvokeAndPushOperations(operation);

        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService?.Refresh();
        OnFramesChanged(FramesChangedType.Reorder, new[] { oldIndex, newIndex });
    }

    public void TogglePlay()
    {
        IsPlaying = !IsPlaying;

        if (FrameRate == 0) // prevent divide by zero
            IsPlaying = false;

        if (IsPlaying)
        {
            _timer.Change(1000 / FrameRate, 1000 / FrameRate);
        }
        else
        {
            _timer.Change(-1, -1);
        }

        CurrentSprite.IsPlaying = IsPlaying;
        OnPlaybackStateChanged();
    }

    public void Stop()
    {
        IsPlaying = false;
        _timer.Change(-1, -1);
        if (CurrentSprite != null)
        {
            CurrentSprite.IsPlaying = false;
            CurrentSprite?.SetFrameIndex(0);
        }

        OnPlaybackStateChanged();
    }

    protected virtual void OnFramesChanged(FramesChangedType changeType, int[] indexes)
    {
        FramesChanged?.Invoke(this, new FramesChangedEventArgs(changeType, indexes));
    }

    protected virtual void OnCurrentFrameChanged()
    {
        _viewPortRefreshService?.Refresh();

        if (CurrentSprite != null)
            _messenger.Send(new SelectedFrameChangedMessage(CurrentFrameIndex, IsPlaying));

        CurrentFrameChanged?.Invoke(this, new SpriteFrameChangedEvenArgs(IsPlaying));
    }

    protected virtual void OnPlaybackStateChanged()
    {
        if (CurrentSprite != null)
            _messenger.Send(new SelectedFrameChangedMessage(CurrentFrameIndex, IsPlaying));

        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PrevFrame()
    {
        CurrentSprite.SetPrevFrame();
        OnCurrentFrameChanged();
    }

    public void NextFrame()
    {
        CurrentSprite.SetNextFrame();
        OnCurrentFrameChanged();
    }

    #endregion

    public void Resize(int newWidth, int newHeight)
    {
        var resizeOperation = new ResizeSpriteOperationBase(CurrentSprite, new SKSize(newWidth, newHeight));
        _operationService.InvokeAndPushOperations(resizeOperation);
        _viewPortRefreshService.Refresh();
    }

    public void Crop(SKSize newSize, float horizontalAnchor, float verticalAnchor)
    {
        var l = horizontalAnchor * (CurrentSprite.Size.Width - newSize.Width);
        var t = verticalAnchor * (CurrentSprite.Size.Height - newSize.Height);
        var bounds = new SKRect(l, t, l + newSize.Width, t + newSize.Height);
        Crop(bounds);
    }

    public void Crop(SKRect newBounds)
    {
        if (newBounds.Width < 0.1 || newBounds.Height < 0.1) return;

        var cropOperation = new CropSpriteOperationBase(CurrentSprite, newBounds);
        _operationService.InvokeAndPushOperations(cropOperation);
        _viewPortRefreshService.Refresh();
    }

    public void FinishEdit()
    {
        CurrentSprite.SetEditMode(false);
    }

    public void Import(ImportData data)
    {
        Resize(data.Size.Width, data.Size.Height);
        if (data.Layers.Count == 0)
            return;

        foreach (var layerPropertiesInfo in data.Layers)
        {
            AddEmptyLayer();
            var layer = SelectedLayer;

            if (data.ReplaceFrames)
                layer.DeleteFrame(0);

            for (var frameIndex = 0; frameIndex < layerPropertiesInfo.Frames.Count; frameIndex++)
            {
                var layerFrameInfo = layerPropertiesInfo.Frames[frameIndex];
                layer.InsertFrameFromBitmap(frameIndex, layerFrameInfo.BitmapProviderFunc());
            }
        }
    }
}