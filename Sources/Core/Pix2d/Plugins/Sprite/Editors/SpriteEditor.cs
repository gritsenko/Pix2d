using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;
using Pix2d.Abstract.Edit;
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Modules.Sprite.Editors;
using Pix2d.Plugins.Sprite.Operations;
using Pix2d.Primitives.Edit;
using Pix2d.Primitives.SpriteEditor;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaSharp;

namespace Pix2d.Plugins.Sprite.Editors;

public class SpriteEditor : ISpriteEditor
{
    public event EventHandler PlaybackStateChanged;

    public event EventHandler LayerChanged;
    public event EventHandler LayersChanged;
    public event EventHandler SelectedLayerChanged;

    public event EventHandler<FramesChangedEventArgs> FramesChanged;
    public event EventHandler<SpriteFrameChangedEvenArgs> CurrentFrameChanged;


    public IDrawingService DrawingService { get; }
    public IViewPortService ViewPortService { get; }
    public IMessenger Messenger { get; }


    public Pix2dSprite.Layer SelectedLayer => CurrentSprite?.SelectedLayer;

    public Pix2dSprite CurrentSprite { get; set; }
        
    public SpriteEditor(IDrawingService drawingService, IViewPortService viewPortService, IMessenger messenger, AppState state)
    {
        DrawingService = drawingService;
        ViewPortService = viewPortService;
        Messenger = messenger;

        Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
        Messenger.Register<ProjectCloseMessage>(this, OnProjectClose);

        _context = SynchronizationContext.Current;

        _timer = new Timer(OnTick, this, -1, -1);
        _projectState = state.CurrentProject;
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
                OnFramesChanged(changeType, new[] {add.FrameIndex});
            }
            else if (e.Operation is DeleteAnimationFrameOperation del)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Add
                    : FramesChangedType.Delete;
                OnFramesChanged(changeType, new[] {del.FrameIndex});
            }
            else if (e.Operation is DuplicateAnimationFrameOperation per)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Delete
                    : FramesChangedType.Add;
                OnFramesChanged(changeType, new[] {per.FrameIndex});
                OnFramesChanged(FramesChangedType.Reset, null);
            }
        }

        if (e.Operation.AffectsNodeStructure || e.Operation is ResizeSpriteOperationBase)
        {
            OnLayersChanged();
            DrawingService.UpdateDrawingTarget();
        }
    }

    private void OnTick(object state)
    {
        if (FrameRate == 0)
        {
            // stop playing on change frame rate to zero
            Dispatcher.UIThread.Invoke(TogglePlay);
            return;
        }

        var frame = CurrentSprite.CurrentFrameIndex;
        CurrentSprite?.OnUpdate(1000f / FrameRate);

        var newFrameIndex = CurrentSprite.CurrentFrameIndex;

        if (newFrameIndex != frame)
        {
            // Changing frames modifies the node structure. If this is done not in the UI thread, it can result
            // in race conditions with processing user input.
            Dispatcher.UIThread.Invoke(() => SetFrameIndex(newFrameIndex));
        }
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
        DrawingService.UpdateDrawingTarget();
    }


    #region layers



    public Pix2dSprite.Layer AddEmptyLayer(Pix2dSprite.Layer addAfter = null)
    {
        var oldSelectedLayer = SelectedLayer;
        var newLayer = CurrentSprite.AddLayer();

        var addLayerOperation = new AddLayerOperation(newLayer.Yield(), oldSelectedLayer);
        addLayerOperation.PushToHistory();

        OnLayersChanged();

        DrawingService.UpdateDrawingTarget();

        return newLayer;
    }

    public void DeleteLayer(Pix2dSprite.Layer layerToDelete = null)
    {
        var layer = layerToDelete ?? SelectedLayer;
        var operation = new DeleteLayerOperation(layer.Yield());
        operation.Invoke();
        //CurrentSprite.DeleteLayer(layer);
        //operation.PushToHistory();
        OnLayersChanged();
    }

    public void DuplicateLayer(Pix2dSprite.Layer layer = null, int insertIndex = -1)
    {
        var oldSelectedLayer = SelectedLayer;
        var newLayer = CurrentSprite.DuplicateLayer(layer ?? SelectedLayer, insertIndex);
        var operation = new DuplicateLayerOperation(newLayer.Yield(), oldSelectedLayer);
        operation.PushToHistory();
        OnLayersChanged();
    }

    public void MergeDownLayer(Pix2dSprite.Layer layer = null)
    {
        if (!CanMergeDownLayer(layer ?? SelectedLayer))
            return;

        var operation = new MergeLayerOperation((layer ?? SelectedLayer).Yield());
        operation.Invoke();

        OnLayersChanged();
    }

    public bool CanMergeDownLayer(Pix2dSprite.Layer layer = null)
    {
        return CurrentSprite?.CanMergeDownLayer(layer ?? SelectedLayer) ?? false;
    }

    public void Rotate(float angle, Pix2dSprite.Layer layer = null)
    {
        if (Math.Abs(angle - 90) < 0.1)
        {
            var selectionEditor = DrawingService.GetSelectionEditor();
            if (selectionEditor.HasSelection)
            {
                selectionEditor.RotateSelection(90);
            }
            else
            {
                RotateCurrentFrame(CurrentSprite);
                DrawingService.UpdateDrawingTarget();
            }

            ViewPortService.Refresh();
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
        CoreServices.OperationService.PushOperation(operation);

        ViewPortService?.Refresh();
        DrawingService.UpdateDrawingTarget();
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
        CoreServices.OperationService.PushOperation(operations.ToArray());
    }

    private void RotateCurrentFrame(Pix2dSprite sprite)
    {
        Debug.Assert(Math.Abs(sprite.Size.Width - sprite.Size.Height) < 0.1);
            
        foreach (var layer in sprite.Layers)
        {
            layer.RotateSourceBitmap(layer.CurrentFrameIndex, true);
        }

        ViewPortService?.Refresh();
        DrawingService.UpdateDrawingTarget();
        OnLayersChanged();
    }

    public void Flip(FlipMode mode, Pix2dSprite.Layer layer = null)
    {
        var selectionEditor = DrawingService.GetSelectionEditor();
        if (selectionEditor.HasSelection)
        {
            selectionEditor.FlipSelection(mode);
        }
        else
        {
            FlipLayer(layer ?? SelectedLayer, mode);
            DrawingService.UpdateDrawingTarget();
        }

        ViewPortService.Refresh();
    }

    public void FlipLayer(Pix2dSprite.Layer layer, FlipMode mode)
    {
        if(!(layer.Nodes[CurrentFrameIndex] is BitmapNode sprite))
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
        CurrentSprite.SendLayerBackward(targetLayer);

        CurrentSprite.SelectLayer(targetLayer);
        OnLayersChanged();
    }

    public void BringLayerForward(Pix2dSprite.Layer layer = null)
    {
        var targetLayer = layer ?? SelectedLayer;
        CurrentSprite.BringLayerForward(targetLayer);
        CurrentSprite.SelectLayer(targetLayer);

        OnLayersChanged();
    }
    public void ReorderLayers(int oldIndex, int newIndex)
    {
        var operation = new ReorderLayersOperation(CurrentSprite, oldIndex, newIndex);
        operation.Invoke();

        DrawingService.UpdateDrawingTarget();
        ViewPortService?.Refresh();
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
        DrawingService.UpdateDrawingTarget();
        SelectedLayerChanged?.Invoke(this, EventArgs.Empty);
        ViewPortService.Refresh();
    }


    #endregion

    #region animation

    private Timer _timer;
    private SynchronizationContext _context;
    private readonly ProjectState _projectState;

    public bool IsPlaying
    {
        get => _projectState.IsAnimationPlaying;
        private set => _projectState.IsAnimationPlaying = value;
    }

    public int CurrentFrameIndex => CurrentSprite.CurrentFrameIndex;

    public bool ShowOnionSkin
    {
        get => CurrentSprite.OnionSkinSettings.IsEnabled;
        set
        {
            if (CurrentSprite.OnionSkinSettings.IsEnabled != value)
            {
                CurrentSprite.OnionSkinSettings.IsEnabled = value;
                ViewPortService.Refresh();
            }
        }
    }

    public int FramesCount => GetFramesCount();

    public int FrameRate
    {
        get { return (int)CurrentSprite.FrameRate; }
        set { CurrentSprite.FrameRate = value; }
    }

    public void SetFrameIndex(int currentFrame)
    {
        if (CurrentSprite == null)
            return;

        DrawingService.SplitCurrentOperation();
        CurrentSprite.SetFrameIndex(currentFrame);
        DrawingService.UpdateDrawingTarget();
        OnCurrentFrameChanged();
    }

    private int GetFramesCount()
    {
        return CurrentSprite?.GetFramesCount() ?? 0;
    }

    public void AddFrame()
    {
        var operation = new AddAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        operation.Invoke();
        OnFramesChanged(FramesChangedType.Add, new[] { CurrentFrameIndex });
    }
    public void DuplicateFrame()
    {
        var operation = new DuplicateAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        operation.Invoke();
        OnFramesChanged(FramesChangedType.Add, new[] { CurrentFrameIndex });
    }

    public void DeleteFrame(int index = -1)
    {
        if (index == -1)
        {
            index = CurrentFrameIndex;
        }

        var operation = new DeleteAnimationFrameOperation(CurrentSprite, index);
        operation.Invoke();

        DrawingService.UpdateDrawingTarget();
        ViewPortService?.Refresh();
        OnFramesChanged(FramesChangedType.Delete, new[] { index });
    }

    public void ReorderFrames(int oldIndex, int newIndex)
    {
        var operation = new ReorderAnimationFramesOperation(CurrentSprite, oldIndex, newIndex);
        operation.Invoke();

        DrawingService.UpdateDrawingTarget();
        ViewPortService?.Refresh();
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
        ViewPortService?.Refresh();

        CurrentFrameChanged?.Invoke(this, new SpriteFrameChangedEvenArgs(IsPlaying));
    }

    protected virtual void OnPlaybackStateChanged()
    {
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
        resizeOperation.Invoke();
        ViewPortService.Refresh();
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
        cropOperation.Invoke();
        ViewPortService.Refresh();
    }

    public void FinishEdit()
    {
        CurrentSprite.SetEditMode(false);
    }

    public void NotifyLayerChanged(Pix2dSprite.Layer layer)
    {
        OnLayerChanged(layer);
    }
    protected virtual void OnLayerChanged(Pix2dSprite.Layer layer)
    {
        LayerChanged?.Invoke(this, new LayerChangedEventArgs(layer));
    }
}