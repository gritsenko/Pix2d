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

    public event EventHandler? PlaybackStateChanged;

    public event EventHandler? LayersChanged;
    public event EventHandler? SelectedLayerChanged;

    public event EventHandler<FramesChangedEventArgs>? FramesChanged;
    public event EventHandler<SpriteFrameChangedEvenArgs>? CurrentFrameChanged;


    private readonly IDrawingService _drawingService;
    private readonly IViewPortRefreshService _viewPortRefreshService;
    private readonly IMessenger _messenger;
    private readonly IOperationService _operationService;

    private readonly Timer _timer;
    private readonly SpriteEditorState _editorState;

    public Pix2dSprite.Layer? SelectedLayer => CurrentSprite?.SelectedLayer;

    public int SelectedLayerIndex => CurrentSprite?.SelectedLayerIndex ?? -1;

    public Pix2dSprite CurrentSprite { get; private set; } = null!;

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
            if (CurrentSprite != null)
            {
                CurrentSprite.OnionSkinSettings.IsEnabled = _editorState.ShowOnionSkin;
                _viewPortRefreshService.Refresh();
            }
        });

        _editorState.WatchFor(x => x.FrameRate, () =>
        {
            if (CurrentSprite != null)
            {
                CurrentSprite.FrameRate = _editorState.FrameRate;

                if (IsPlaying)
                    ArmFrameTimer();

                _viewPortRefreshService.Refresh();
            }
        });

        _operationTimer = new Timer(OnOperationTimerTick, this, -1, -1);
    }

    private void OnOperationTimerTick(object? state)
    {
        // Same reasoning as the playback timer below: this fires on a threadpool thread, and pushing an
        // operation touches UI-thread state — the undo history. Doing it here raced whatever the user was
        // drawing at the same moment and threw "Collection was modified after the enumerator was
        // instantiated" out of OperationService's undo stack (appstat, 3.11.2). Post, not Invoke: the tick
        // has nothing to wait for, and SetDebouncedOperation holds _updateOperationLock across a
        // PerformPendingOperation call of its own.
        Dispatcher.UIThread.Post(PerformPendingOperation);
    }

    private void OnProjectClose(ProjectCloseMessage? obj)
    {
        Stop();
    }

    private void OnOperationInvoked(OperationInvokedMessage? e)
    {
        if (e?.OperationType != OperationEventType.Perform)
        {
            if (e?.Operation is AddAnimationFrameOperation add)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Delete
                    : FramesChangedType.Add;
                OnFramesChanged(changeType, [add.FrameIndex]);
            }
            else if (e?.Operation is DeleteAnimationFrameOperation del)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Add
                    : FramesChangedType.Delete;
                OnFramesChanged(changeType, [del.FrameIndex]);
            }
            else if (e?.Operation is DuplicateAnimationFrameOperation per)
            {
                var changeType = e.OperationType == OperationEventType.Undo
                    ? FramesChangedType.Delete
                    : FramesChangedType.Add;
                OnFramesChanged(changeType, [per.FrameIndex]);
                OnFramesChanged(FramesChangedType.Reset, null!);
            }
        }

        if (e?.Operation.AffectsNodeStructure == true || e?.Operation is ResizeSpriteOperationBase)
        {
            OnLayersChanged();
            _drawingService.UpdateDrawingTarget();
        }
    }

    private void OnTick(object? state)
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
        Dispatcher.UIThread.Invoke(() =>
        {
            SetFrameIndex(CurrentSprite?.NextFrameIndex ?? 0);

            // One-shot chain: the next interval is the *new* frame's duration. Re-check IsPlaying —
            // SetFrameIndex can run a pending operation, and Stop may have fired in between.
            if (IsPlaying)
                ArmFrameTimer();
        });
    }


    public void SetTargetNode(SKNode node)
    {
        var oldSprite = CurrentSprite;

        oldSprite?.SetEditMode(false);

        CurrentSprite = node as Pix2dSprite ?? throw new ArgumentException("Node must be a Pix2dSprite");

        CurrentSprite?.SetEditMode(true);

        if (oldSprite != CurrentSprite)
        {
            OnFramesChanged(FramesChangedType.Reset, null!);
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

    /// <summary>
    /// Renames a layer as one undoable step. A blank name is rejected rather than stored — an
    /// empty title would leave the layer tile unlabelled with no way to tell it apart.
    /// </summary>
    public void RenameLayer(Pix2dSprite.Layer layer, string name)
    {
        var newName = name.Trim();
        if (string.IsNullOrEmpty(newName) || newName == layer.Name)
            return;

        var operation = new RenameNodeOperation([layer]);
        layer.Name = newName;
        operation.SetFinalData();
        _operationService.PushOperations(operation);
    }


    public Pix2dSprite.Layer AddEmptyLayer(Pix2dSprite.Layer? addAfter = null)
    {
        var oldSelectedLayer = SelectedLayer;
        var newLayer = CurrentSprite?.AddLayer() ?? throw new InvalidOperationException("No current sprite");

        var addLayerOperation = new AddLayerOperation(newLayer.Yield(), oldSelectedLayer!);
        _operationService.PushOperations(addLayerOperation);
        OnLayersChanged();

        _drawingService.UpdateDrawingTarget();

        return newLayer;
    }

    public void DeleteLayer(Pix2dSprite.Layer? layerToDelete = null)
    {
        if (CurrentSprite?.LayersCount <= 1)
            return;

        var layer = layerToDelete ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");
        var operation = new DeleteLayerOperation(layer.Yield());
        _operationService.InvokeAndPushOperations(operation);
        //CurrentSprite.DeleteLayer(layer);
        //operation.PushToHistory();
        OnLayersChanged();
    }

    public void DuplicateLayer(Pix2dSprite.Layer? layer = null, int insertIndex = -1)
    {
        var oldSelectedLayer = SelectedLayer;
        var layerToDuplicate = layer ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");
        var newLayer = CurrentSprite?.DuplicateLayer(layerToDuplicate, insertIndex) ?? throw new InvalidOperationException("No current sprite");
        var operation = new DuplicateLayerOperation(newLayer.Yield(), oldSelectedLayer!);
        _operationService.PushOperations(operation);
        OnLayersChanged();
    }

    public void MergeDownLayer(Pix2dSprite.Layer? layer = null)
    {
        var layerToMerge = layer ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");
        if (!CanMergeDownLayer(layerToMerge))
            return;

        var operation = new MergeLayerOperation(layerToMerge.Yield());
        _operationService.InvokeAndPushOperations(operation);

        OnLayersChanged();
    }

    public void SetOpacity(float newOpacity)
    {
        var selectedLayer = SelectedLayer ?? throw new InvalidOperationException("No layer selected");
        var operation = new ChangeOpacityOperation([selectedLayer]);
        selectedLayer.Opacity = newOpacity;
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

    public bool CanMergeDownLayer(Pix2dSprite.Layer? layer = null)
    {
        var layerToCheck = layer ?? SelectedLayer;
        if (layerToCheck == null || CurrentSprite == null)
            return false;
        return CurrentSprite.CanMergeDownLayer(layerToCheck!);
    }

    public void Rotate(float angle, Pix2dSprite.Layer? layer = null)
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
                if (CurrentSprite != null)
                {
                    RotateCurrentFrame(CurrentSprite);
                    _drawingService.UpdateDrawingTarget();
                }
            }

            _viewPortRefreshService.Refresh();
        }
    }

    public void RotateSprite()
    {
        if (CurrentSprite == null)
            return;

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
        if (CurrentSprite == null)
            return;

        var operations = new List<IEditOperation>();
        if (Math.Abs(CurrentSprite.Size.Width - CurrentSprite.Size.Height) > 0.1)
        {
            var size = Math.Max(CurrentSprite.Size.Width, CurrentSprite.Size.Height);
            var resizeOperation = new ResizeSpriteOperationBase(CurrentSprite, new SKSize(size, size));
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

    public void Flip(FlipMode mode, Pix2dSprite.Layer? layer = null)
    {
        var selectionEditor = _drawingService.GetSelectionEditor();
        if (selectionEditor.HasSelection)
        {
            selectionEditor.FlipSelection(mode);
        }
        else if (CurrentSprite != null)
        {
            var layerToFlip = layer ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");

            var operation = new EditFrameOperation(CurrentSprite);
            FlipLayer(layerToFlip, mode);
            operation.SetFinalData();
            _operationService.PushOperations(operation);

            _drawingService.UpdateDrawingTarget();
        }

        _viewPortRefreshService.Refresh();
    }

    public void FlipLayer(Pix2dSprite.Layer layer, FlipMode mode)
    {
        layer.FlipSourceBitmap(CurrentFrameIndex, mode);
    }

    public void SendLayerBackward(Pix2dSprite.Layer? layer = null)
    {
        var targetLayer = layer ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");
        if (targetLayer.Index - 1 < 0)
            return;

        ReorderLayers(targetLayer.Index, targetLayer.Index - 1);
    }

    public void BringLayerForward(Pix2dSprite.Layer? layer = null)
    {
        var targetLayer = layer ?? SelectedLayer ?? throw new InvalidOperationException("No layer selected");

        if (targetLayer.Index + 1 >= targetLayer.Parent!.Nodes.Count)
            return;

        ReorderLayers(targetLayer.Index, targetLayer.Index + 1);
    }
    public void ReorderLayers(int oldIndex, int newIndex)
    {
        if (CurrentSprite == null)
            return;

        var operation = new ReorderLayersOperation(CurrentSprite, oldIndex, newIndex);
        _operationService.InvokeAndPushOperations(operation);

        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService?.Refresh();
        OnLayersChanged();
    }

    public void SelectLayer(Pix2dSprite.Layer layer)
    {
        CurrentSprite?.SelectLayer(layer);
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
        _viewPortRefreshService?.Refresh();
    }


    #endregion

    #region animation


    public bool IsPlaying
    {
        get => _editorState.IsPlayingAnimation;
        private set => _editorState.IsPlayingAnimation = value;
    }

    public int CurrentFrameIndex => CurrentSprite?.CurrentFrameIndex ?? 0;

    public int FramesCount => GetFramesCount();

    public int FrameRate
    {
        get => CurrentSprite != null ? (int)CurrentSprite.FrameRate : 0;
        set
        {
            if (CurrentSprite != null)
                CurrentSprite.FrameRate = value;
        }
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
        if (CurrentSprite == null)
            return;

        var operation = new AddAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        _operationService.InvokeAndPushOperations(operation);
        OnFramesChanged(FramesChangedType.Add, [CurrentFrameIndex]);
    }

    public void AddFrameAtEnd()
    {
        if (CurrentSprite == null)
            return;

        //previousIndex == -1 means add to end of list
        var operation = new AddAnimationFrameOperation(CurrentSprite, -1);
        _operationService.InvokeAndPushOperations(operation);
        OnFramesChanged(FramesChangedType.Add, [CurrentFrameIndex]);
    }
    public void DuplicateFrame()
    {
        if (CurrentSprite == null)
            return;

        var operation = new DuplicateAnimationFrameOperation(CurrentSprite, CurrentFrameIndex);
        _operationService.InvokeAndPushOperations(operation);
        OnFramesChanged(FramesChangedType.Add, [CurrentFrameIndex]);
    }

    public void DeleteFrame(int index = -1)
    {
        if (CurrentSprite?.GetFramesCount() <= 1)
        {
            return;
        }

        if (index == -1)
        {
            index = CurrentFrameIndex;
        }

        if (CurrentSprite == null)
            return;

        var operation = new DeleteAnimationFrameOperation(CurrentSprite, index);
        _operationService.InvokeAndPushOperations(operation);

        _drawingService.UpdateDrawingTarget();
        _viewPortRefreshService?.Refresh();
        OnFramesChanged(FramesChangedType.Delete, [index]);
    }

    public void ReorderFrames(int oldIndex, int newIndex)
    {
        if (CurrentSprite == null)
            return;

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
            ArmFrameTimer();
        }
        else
        {
            _timer.Change(-1, -1);
        }

        if (CurrentSprite != null)
            CurrentSprite.IsPlaying = IsPlaying;
        OnPlaybackStateChanged();
    }

    /// <summary>
    /// Arms the playback timer for the frame currently showing. Frames can carry individual durations
    /// (<see cref="Pix2dSprite.GetFrameDurationMs"/>), so playback is a chain of one-shots re-armed after
    /// each advance rather than one fixed-period timer — otherwise the in-app preview would disagree
    /// with the exported metadata. With no overrides every interval is 1000/FrameRate, i.e. exactly the
    /// previous behaviour.
    /// </summary>
    private void ArmFrameTimer()
    {
        var due = CurrentSprite?.GetFrameDurationMs(CurrentFrameIndex) ?? (int)(1000 / Math.Max(1f, FrameRate));
        _timer.Change(Math.Max(1, due), Timeout.Infinite);
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

    protected virtual void OnFramesChanged(FramesChangedType changeType, int[]? indexes)
    {
        FramesChanged?.Invoke(this, new FramesChangedEventArgs(changeType, indexes!));
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

    #region animation metadata (tags, frame durations, export anchors)

    // Every method here is one undoable gesture: capture, mutate, commit. They all run through
    // EditAnimationMetaOperation so the whole metadata block is restored as a unit — see that class for
    // why the edits are snapshotted rather than inverted.

    /// <summary>Sets (or with null clears) the current sprite's override duration for one frame.</summary>
    public void SetFrameDuration(int frameIndex, int? milliseconds)
        => EditAnimationMeta(sprite => sprite.SetFrameDurationMs(frameIndex, milliseconds));

    /// <summary>
    /// Creates a tag covering the current frame, with a name that doesn't collide with an existing one.
    /// Widening the range afterwards is the user's next move — deliberately cheaper than requiring a
    /// range selection up front.
    /// </summary>
    public SpriteAnimationTag? AddAnimationTag(string? name = null)
    {
        SpriteAnimationTag? created = null;

        EditAnimationMeta(sprite =>
        {
            var frame = Math.Max(0, Math.Min(CurrentFrameIndex, sprite.GetFramesCount() - 1));
            created = new SpriteAnimationTag
            {
                Name = string.IsNullOrWhiteSpace(name) ? GetUniqueTagName(sprite) : name.Trim(),
                From = frame,
                To = frame
            };

            sprite.AnimationTags ??= [];
            sprite.AnimationTags.Add(created);
        });

        return created;
    }

    public void RemoveAnimationTag(SpriteAnimationTag tag)
        => EditAnimationMeta(sprite => sprite.AnimationTags?.Remove(tag));

    /// <summary>
    /// Applies an edited tag. The range is clamped into the sprite and ordered, so the UI can bind two
    /// independent numeric fields without having to police From &lt;= To itself.
    /// </summary>
    public void UpdateAnimationTag(
        SpriteAnimationTag tag, string name, int from, int to, SpriteAnimationDirection direction)
        => EditAnimationMeta(sprite =>
        {
            var target = sprite.AnimationTags?.FirstOrDefault(t => ReferenceEquals(t, tag));
            if (target == null)
                return;

            var lastFrame = Math.Max(0, sprite.GetFramesCount() - 1);
            target.Name = string.IsNullOrWhiteSpace(name) ? target.Name : name.Trim();
            target.From = Math.Clamp(Math.Min(from, to), 0, lastFrame);
            target.To = Math.Clamp(Math.Max(from, to), 0, lastFrame);
            target.Direction = direction;
        });

    public void SetExportPivot(SKPoint? pivot)
        => EditAnimationMeta(sprite => sprite.ExportPivot = pivot);

    public void SetNineSlice(NineSliceMargins? margins)
        => EditAnimationMeta(sprite => sprite.NineSlice = margins);

    private void EditAnimationMeta(Action<Pix2dSprite> edit)
    {
        if (CurrentSprite is not { } sprite)
            return;

        var operation = new EditAnimationMetaOperation(sprite);
        edit(sprite);
        sprite.NormalizeAnimationTags();
        operation.SetFinalData();
        _operationService.PushOperations(operation);

        _viewPortRefreshService?.Refresh();
    }

    private static string GetUniqueTagName(Pix2dSprite sprite)
    {
        var existing = sprite.AnimationTags?.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        for (var i = 1; ; i++)
        {
            var candidate = $"Tag {i}";
            if (existing.Add(candidate))
                return candidate;
        }
    }

    #endregion

    public void PrevFrame()
    {
        CurrentSprite?.SetPrevFrame();
        OnCurrentFrameChanged();
    }

    public void NextFrame()
    {
        CurrentSprite?.SetNextFrame();
        OnCurrentFrameChanged();
    }

    #endregion

    public void Resize(int newWidth, int newHeight)
    {
        if (CurrentSprite == null)
            return;

        var resizeOperation = new ResizeSpriteOperationBase(CurrentSprite, new SKSize(newWidth, newHeight));
        _operationService.InvokeAndPushOperations(resizeOperation);
        _viewPortRefreshService.Refresh();
    }

    public void Crop(SKSize newSize, float horizontalAnchor, float verticalAnchor)
    {
        if (CurrentSprite == null)
            return;

        var l = horizontalAnchor * (CurrentSprite.Size.Width - newSize.Width);
        var t = verticalAnchor * (CurrentSprite.Size.Height - newSize.Height);
        var bounds = new SKRect(l, t, l + newSize.Width, t + newSize.Height);
        Crop(bounds);
    }

    public void Crop(SKRect newBounds)
    {
        if (newBounds.Width < 0.1 || newBounds.Height < 0.1) return;

        if (CurrentSprite == null)
            return;

        var cropOperation = new CropSpriteOperationBase(CurrentSprite, newBounds);
        _operationService.InvokeAndPushOperations(cropOperation);
        _viewPortRefreshService.Refresh();
    }

    public void FinishEdit()
    {
        CurrentSprite?.SetEditMode(false);
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
            if (layer == null)
                throw new InvalidOperationException("No layer selected");

            if (data.ReplaceFrames)
                layer.DeleteFrame(0);

            for (var frameIndex = 0; frameIndex < layerPropertiesInfo.Frames.Count; frameIndex++)
            {
                var layerFrameInfo = layerPropertiesInfo.Frames[frameIndex];
                var bitmap = layerFrameInfo.BitmapProviderFunc?.Invoke() ?? new SKBitmap();
                // Normalize to the sprite size: InsertFrameFromBitmap throws when sizes differ
                // (e.g. importing images of mixed size as layers).
                layer.InsertFrameFromBitmap(frameIndex, SpriteImportApplier.NormalizeBitmap(bitmap, data.Size));
            }
        }
    }
}