#nullable enable
using Pix2d.Abstract.Selection;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes.Thumbs;
using Pix2d.InteractiveNodes.Thumbs.Resize;
using Pix2d.Operations;
using Pix2d.Primitives.Edit;
using Pix2d.Selection;
using Pix2d.Services;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes;

public class FrameEditorNode : SKNode
{
    public event EventHandler? SelectionEditStarted;
    public event EventHandler? SelectionEditing;
    public event EventHandler? SelectionEdited;
    public event EventHandler? SelectionEditCanceled;

    private readonly MoveThumbNode _moveThumb;
    private readonly ResizeThumbSingleNode[] _sizeThumb = new ResizeThumbSingleNode[4];
    private NodesSelection? _selection;
    private SKPoint _initialPos;
    private SKSize _initialSize;
    private float _initialRotation;
    private bool _forceIsChanged = false;
    private bool _allowResize = true;
    private RotateThumbNode _rotateThumb;
    private readonly LineHighlightNode _highlightNode;
    private bool _dragSessionActive;
    private bool _dragMoved;

    public NodeReparentMode ReparentMode { get; set; }

    public Func<IAspectSnapper>? AspectSnapperProviderFunc { get; set; }


    public bool AllowResize
    {
        get => _allowResize;
        set
        {
            _allowResize = value;
            UpdateThumbs();
        }
    }

    private bool _contourOnly;
    private bool _frameResizeMode;

    /// <summary>
    /// When true, the editor renders in contour-edit mode: only the marching-ants outline is shown (drawn by
    /// the move thumb), all resize and rotate manipulators are hidden. The move thumb stays interactive so
    /// the user can still drag the marquee to reshape what's selected — that's the contour-mode contract:
    /// no transforming of underlying pixels, just shifting the selection region. Set to false to expose the
    /// full transform handles (resize circles + rotate).
    /// </summary>
    public bool ContourOnly
    {
        get => _contourOnly;
        set
        {
            if (_contourOnly == value) return;
            _contourOnly = value;
            UpdateThumbs();
        }
    }

    /// <summary>
    /// Frame-resize mode (crop tool). Layered ON TOP of <see cref="ContourOnly"/>: when both are true the
    /// move thumb still draws marching ants (no pixel lift), but the resize handles are forced visible and
    /// rendered in the contour/black styling — signalling that dragging them resizes the marquee region
    /// itself, not transforming any underlying pixels. The rotate thumb stays hidden because rotating a
    /// crop frame doesn't fit the Photoshop model. No-op when <see cref="ContourOnly"/> is false (transform
    /// mode owns its own resize affordance).
    /// </summary>
    public bool FrameResizeMode
    {
        get => _frameResizeMode;
        set
        {
            if (_frameResizeMode == value) return;
            _frameResizeMode = value;
            UpdateThumbs();
        }
    }

    public SKRect SelectionBounds => _moveThumb.GetBoundingBox();

    public bool EditStarted { get; set; }
    public bool IsChanged => _initialPos != _moveThumb.Position || _initialSize != _moveThumb.Size || _forceIsChanged || Math.Abs(_moveThumb.Rotation - _initialRotation) > 0.01;

    public FrameEditorNode()
    {
        _moveThumb = new MoveThumbNode() { SnapToPixels = true, AxisLockProviderFunc = GetAxisLock };
        _sizeThumb[0] = new LeftTopResizeThumbSingleNode() { SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };
        _sizeThumb[1] = new RightBottomResizeThumbSingleNode() { SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };
        _sizeThumb[2] = new RightTopResizeThumbSingleNode() { SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };
        _sizeThumb[3] = new LeftBottomResizeThumbSingleNode() { SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };

        _rotateThumb = new RotateThumbNode() { SnapToPixels = false, AngleLockProviderFunc = GetAngleLock };

        _highlightNode = new LineHighlightNode() { IsVisible = false };

        _moveThumb.DragStarted += MoveThumb_DragStarted;
        _moveThumb.DragDelta += Thumb_DragDelta;
        _moveThumb.DragComplete += ThumbOnDragComplete;
        _moveThumb.PointerReleased += ThumbOnPointerReleased;

        _sizeThumb[0].DragDelta += Thumb_DragDelta;
        _sizeThumb[1].DragDelta += Thumb_DragDelta;
        _sizeThumb[2].DragDelta += Thumb_DragDelta;
        _sizeThumb[3].DragDelta += Thumb_DragDelta;

        _sizeThumb[0].DragStarted += SizeThumb_DragStarted;
        _sizeThumb[1].DragStarted += SizeThumb_DragStarted;
        _sizeThumb[2].DragStarted += SizeThumb_DragStarted;
        _sizeThumb[3].DragStarted += SizeThumb_DragStarted;

        _sizeThumb[0].DragComplete += ThumbOnDragComplete;
        _sizeThumb[1].DragComplete += ThumbOnDragComplete;
        _sizeThumb[2].DragComplete += ThumbOnDragComplete;
        _sizeThumb[3].DragComplete += ThumbOnDragComplete;
        _sizeThumb[0].PointerReleased += ThumbOnPointerReleased;
        _sizeThumb[1].PointerReleased += ThumbOnPointerReleased;
        _sizeThumb[2].PointerReleased += ThumbOnPointerReleased;
        _sizeThumb[3].PointerReleased += ThumbOnPointerReleased;

        _rotateThumb.DragStarted += RotateThumb_DragStarted;
        _rotateThumb.DragDelta += Thumb_DragDelta;
        _rotateThumb.DragComplete += ThumbOnDragComplete;
        _rotateThumb.PointerReleased += ThumbOnPointerReleased;

        Nodes.Add(_highlightNode);
        Nodes.Add(_moveThumb);
        Nodes.Add(_sizeThumb[0]);
        Nodes.Add(_sizeThumb[1]);
        Nodes.Add(_sizeThumb[2]);
        Nodes.Add(_sizeThumb[3]);
        Nodes.Add(_rotateThumb);

        UpdateThumbs();
    }

    private void SizeThumb_DragStarted(object? sender, DragStartedEventArgs e)
    {
        BeginDragSession();
        _selection?.InitOperation<ResizeOperation>();
    }

    private void MoveThumb_DragStarted(object? sender, DragStartedEventArgs e)
    {
        BeginDragSession();
        _selection?.InitOperation<MoveOperation>();
    }
    private void RotateThumb_DragStarted(object? sender, DragStartedEventArgs e)
    {
        BeginDragSession();
        _selection?.InitOperation<RotateOperation>();
    }

    private void BeginDragSession()
    {
        _dragSessionActive = true;
        _dragMoved = false;
        FreezeContourForDrag();
        OnSelectionEditStarted();
    }

    private void ThumbOnDragComplete(object? sender, DragCompletedEventArgs e)
    {
        _dragSessionActive = false;
        // Unfreeze first so SelectionEdited consumers see the final state, then re-sync the contour to the
        // pixel-snapped frame the thumbs left behind. Doing this in one step on release avoids the visual
        // wobble of recalculating the dashed outline every drag-delta.
        UnfreezeContourAfterDrag();
        OnSelectionEdited();
        _selection?.FinishOperation();
    }

    private void ThumbOnPointerReleased(object? sender, PointerActionEventArgs e)
    {
        if (!_dragSessionActive || _dragMoved)
            return;

        _dragSessionActive = false;
        EditStarted = false;
        UnfreezeContourAfterDrag();
        SelectionEditCanceled?.Invoke(this, EventArgs.Empty);
    }

    private void FreezeContourForDrag() => _highlightNode.FreezeTransformUpdates = true;

    private void UnfreezeContourAfterDrag()
    {
        _highlightNode.FreezeTransformUpdates = false;
        _highlightNode.SyncTransformToFrame();
    }

    private void Thumb_DragDelta(object? sender, DragDeltaEventArgs e)
    {
        _dragMoved = true;

        var skip = sender as NodeManipulateThumbBase;

        foreach (var thumb in Nodes.OfType<NodeManipulateThumbBase>())
        {
            if (thumb != skip)
            {
                thumb.UpdateToTargets();
            }
        }
        OnSelectionEditing();
    }

    public void SetSelection(INodesSelection selection, SKPath? highlightPath = null, IReadOnlyList<IReadOnlyList<SKPoint>>? highlightContours = null)
    {
        EditStarted = false;
        _selection = selection as NodesSelection;
        _highlightNode.SetSelection(_selection, highlightPath, highlightContours);

        this.IsVisible = _selection?.Nodes.Any() ?? false;

        // When a real contour is supplied (lasso / same-colour), LineHighlightNode renders it; the move
        // thumb must not double-draw a bounding rect on top.
        _moveThumb.HasCustomContour = highlightPath != null;
        _moveThumb.SetCustomContourPath(CreateLocalContourPath(highlightPath, highlightContours));

        foreach (var thumb in Nodes.OfType<NodeManipulateThumbBase>())
        {
            thumb.TargetSelection = _selection;
            thumb.UpdateToTargets();
        }

        UpdateThumbs();

        ResetIsChanged();
    }

    private static SKPath? CreateLocalContourPath(SKPath? highlightPath, IReadOnlyList<IReadOnlyList<SKPoint>>? highlightContours)
    {
        if (highlightPath == null)
            return null;

        var bounds = highlightContours == null || highlightContours.Count == 0
            ? highlightPath.Bounds
            : GetContourBounds(highlightContours);

        var localPath = new SKPath();
        highlightPath.Transform(SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top), localPath);
        return localPath;
    }

    private static SKRect GetContourBounds(IReadOnlyList<IReadOnlyList<SKPoint>> contours)
    {
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        foreach (var contour in contours)
        {
            foreach (var point in contour)
            {
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
            return SKRect.Empty;

        return new SKRect(minX, minY, maxX, maxY);
    }

    private void UpdateThumbs()
    {
        // Resize & rotate manipulators belong to transform mode by default — in contour mode the marquee is
        // just a region selector and resize/rotate would imply pixel transformation, which is
        // PixelTransformTool's job. Exception: crop tool's frame-resize mode forces resize handles visible
        // even in contour mode (rendered in black) so the user can adjust the crop rectangle without
        // lifting pixels. The move thumb stays visible (it draws the marching-ants outline) AND interactive
        // in both modes so the user can drag the marquee around to reshape it.
        var showResize = _allowResize && this.IsVisible && (!_contourOnly || _frameResizeMode);
        foreach (var resizeThumbSingleNode in _sizeThumb)
        {
            resizeThumbSingleNode.IsVisible = showResize;
            resizeThumbSingleNode.ContourOnly = _contourOnly;
            resizeThumbSingleNode.Opacity = 50;
        }

        _rotateThumb.IsVisible = this.IsVisible && !_contourOnly;

        _moveThumb.ContourOnly = _contourOnly;
        _moveThumb.IsInteractive = true;
    }

    private void ResetIsChanged()
    {
        _forceIsChanged = false;
        _initialPos = _moveThumb.Position;
        _initialSize = _moveThumb.Size;
        _initialRotation = _moveThumb.Rotation;
    }

    public void Hide()
    {
        ResetIsChanged();
        IsVisible = false;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        // DrawBoundingBox(canvas, vp, 2, SKColors.BlueViolet);
    }

    protected virtual void OnSelectionEdited()
    {
        _selection?.Invalidate();
        SelectionEdited?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnSelectionEditing()
    {
        _selection?.Invalidate();
        SelectionEditing?.Invoke(this, EventArgs.Empty);
    }

    public bool GetAspectLock()
    {
        return _selection?.LockAspect ?? false || (AspectSnapperProviderFunc?.Invoke()?.IsAspectLocked == true);
    }
    public bool GetAxisLock()
    {
        return AspectSnapperProviderFunc?.Invoke()?.IsAspectLocked ?? false;
    }
    public bool GetAngleLock()
    {
        return AspectSnapperProviderFunc?.Invoke()?.IsAspectLocked ?? false;
    }

    public void ActivateMoveThumb()
    {
        _moveThumb.OnPointerPressed(
            new PointerActionEventArgs(PointerActionType.Pressed, SKInput.Current.Pointer!,
                SKInput.Current.GetModifiers()!), 0);
    }

    protected virtual void OnSelectionEditStarted()
    {
        EditStarted = true;
        SelectionEditStarted?.Invoke(this, EventArgs.Empty);
    }


    public void SetIsChanged()
    {
        _forceIsChanged = true;
    }

    public void ManipulateSelection(Action action)
    {
        OnSelectionEditStarted();
        action?.Invoke();
        OnSelectionEdited();
    }

    public void Rotate(int angle)
    {
        if (_selection == null) return;
        OnSelectionEditStarted();
        _selection.Rotation += angle;
        OnSelectionEdited();
    }

    public void ResetEdit()
    {
        if (_selection == null) return;
        _selection.SetPosition(_initialPos);
        _selection.SetRotation(_initialRotation);
        _selection.SetSize(_initialSize);
    }
}