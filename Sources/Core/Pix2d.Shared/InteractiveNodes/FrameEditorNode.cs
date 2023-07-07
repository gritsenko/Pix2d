using System;
using System.Linq;
using Pix2d.Abstract.Selection;
using Pix2d.CommonNodes;
using Pix2d.CommonNodes.Controls.Thumbs;
using Pix2d.CommonNodes.Controls.Thumbs.Resize;
using Pix2d.Operations;
using Pix2d.Primitives.Edit;
using Pix2d.Services;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.InteractiveNodes
{
    public class FrameEditorNode : SKNode
    {
        public event EventHandler SelectionEditStarted;
        public event EventHandler SelectionEditing;
        public event EventHandler SelectionEdited;

        private readonly MoveThumbNode _moveThumb;
        private readonly ResizeThumbSingleNode[] _sizeThumb = new ResizeThumbSingleNode[4];
        private NodesSelection _selection;
        private SKPoint _initialPos;
        private SKSize _initialSize;
        private float _initialRotation;
        private bool _forceIsChanged = false;
        private bool _allowResize = true;
        private RotateThumbNode _rotateThumb;
        private readonly LineHighlightNode _highlightNode;

        public NodeReparentMode ReparentMode { get; set; }

        public Func<IAspectSnapper> AspectSnapperProviderFunc { get; set; }


        public bool AllowResize
        {
            get => _allowResize;
            set
            {
                _allowResize = value;
                UpdateThumbs();
            }
        }

        public SKRect SelectionBounds => _moveThumb.GetBoundingBox();

        public bool EditStarted { get; set; }
        public bool IsChanged => _initialPos != _moveThumb.Position || _initialSize != _moveThumb.Size || _forceIsChanged || Math.Abs(_moveThumb.Rotation - _initialRotation) > 0.01; 

        public FrameEditorNode()
        {
            _moveThumb = new MoveThumbNode(){SnapToPixels = true, AxisLockProviderFunc = GetAxisLock };
            _sizeThumb[0] = new LeftTopResizeThumbSingleNode() {SnapToPixels = true, AspectLockProviderFunc = GetAspectLock};
            _sizeThumb[1] = new RightBottomResizeThumbSingleNode() {SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };
            _sizeThumb[2] = new RightTopResizeThumbSingleNode() {SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };
            _sizeThumb[3] = new LeftBottomResizeThumbSingleNode() {SnapToPixels = true, AspectLockProviderFunc = GetAspectLock };

            _rotateThumb = new RotateThumbNode(){ SnapToPixels = false, AngleLockProviderFunc = GetAspectLock };

            _highlightNode = new LineHighlightNode();

            _moveThumb.DragStarted += MoveThumb_DragStarted;
            _moveThumb.DragDelta += Thumb_DragDelta;
            _moveThumb.DragComplete += ThumbOnDragComplete;

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

            _rotateThumb.DragStarted += RotateThumb_DragStarted;
            _rotateThumb.DragDelta += Thumb_DragDelta;
            _rotateThumb.DragComplete += ThumbOnDragComplete;

            Nodes.Add(_highlightNode);
            Nodes.Add(_moveThumb);
            Nodes.Add(_sizeThumb[0]);
            Nodes.Add(_sizeThumb[1]);
            Nodes.Add(_sizeThumb[2]);
            Nodes.Add(_sizeThumb[3]);
            Nodes.Add(_rotateThumb);

            UpdateThumbs();
        }

        private void SizeThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _selection.InitOperation<ResizeOperation>();
            OnSelectionEditStarted();
        }

        private void MoveThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _selection.InitOperation<MoveOperation>();
            if(!EditStarted)
                OnSelectionEditStarted();
        }
        private void RotateThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _selection.InitOperation<RotateOperation>();
            if(!EditStarted)
                OnSelectionEditStarted();
        }

        private void ThumbOnDragComplete(object sender, DragCompletedEventArgs e)
        {
            OnSelectionEdited();
            _selection.FinishOperation();
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var skip = sender as NodeManipulateThumbBase;

            foreach (var thumb in Nodes.OfType<NodeManipulateThumbBase>())
            {
                if (thumb != skip)
                {
                    thumb.UpdateToTargets();
                }
            }

            if (sender is MoveThumbNode)
            {
                _selection.UpdateParents(ReparentMode);
            }

            OnSelectionEditing();
        }

        public void SetSelection(INodesSelection selection, SKPath highlightPath = null)
        {
            EditStarted = false;
            _selection = selection as NodesSelection;
            _highlightNode.SetSelection(_selection, highlightPath);

            this.IsVisible = _selection?.Nodes.Any() ?? false;

            foreach (var thumb in Nodes.OfType<NodeManipulateThumbBase>())
            {
                thumb.TargetSelection = _selection;
                thumb.UpdateToTargets();
            }

            UpdateThumbs();

            ResetIsChanged();
        }

        private void UpdateThumbs()
        {
            foreach (var resizeThumbSingleNode in _sizeThumb)
            {
                resizeThumbSingleNode.IsVisible = _allowResize && this.IsVisible;
                resizeThumbSingleNode.Opacity = 50;
            }
        }

        private void ResetIsChanged()
        {
            _forceIsChanged = false;
            _initialPos = _moveThumb.Position;
            _initialSize = _moveThumb.Size;
            _initialRotation = _moveThumb.Rotation;
        }

        public override void Hide()
        {
            ResetIsChanged();
            base.Hide();
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {
            // DrawBoundingBox(canvas, vp, 2, SKColors.BlueViolet);
        }

        protected virtual void OnSelectionEdited()
        {
            _selection.Invalidate();
            SelectionEdited?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSelectionEditing()
        {
            _selection.Invalidate();
            SelectionEditing?.Invoke(this, EventArgs.Empty);
        }

        public bool GetAspectLock()
        {
            return _selection.LockAspect || AspectSnapperProviderFunc?.Invoke().IsAspectLocked == true;
        }
        public bool GetAxisLock()
        {
            return AspectSnapperProviderFunc?.Invoke().IsAspectLocked ?? false;
        }

        public void ActivateMoveThumb()
        {
            _moveThumb.OnPointerPressed(
                new PointerActionEventArgs(PointerActionType.Pressed, SKInput.Current.Pointer,
                    SKInput.Current.GetModifiers()), 0);
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
            OnSelectionEditStarted();
            _selection.Rotation += angle;
            OnSelectionEdited();
        }
    }
}
