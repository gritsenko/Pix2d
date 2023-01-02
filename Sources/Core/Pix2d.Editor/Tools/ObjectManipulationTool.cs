using System;
using System.Threading.Tasks;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes.Controls;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Tools
{
    public class ObjectManipulationTool : BaseTool
    {
        public IEditService EditService { get; }
        public ISelectionService SelectionService { get; }
        public ISceneService SceneService { get; }
        public IMessenger Messenger { get; }
        public bool IncrementalSelectionMode => SKInput.Current.GetModifiers() == KeyModifier.Shift;

        private Frame _highlightNodeFrame = new Frame() { StrokeColor = new SKColor(0xff54a1ea), StrokeThickness = 2f };
        private Frame _selectionFrame = new Frame() { StrokeColor = new SKColor(0xff54a1ea), StrokeThickness = 1f };
        private SKNode _scene;
        private bool _selectedOnPressed;
        private SKPoint _startPos;
        private SKPoint _endPos;
        private SKPoint _delta;

        public override string DisplayName => "Objects tool";

        public ObjectManipulationTool(IEditService editService, ISelectionService selectionService, ISceneService sceneService, IMessenger messenger)
        {
            EditService = editService;
            SelectionService = selectionService;
            SceneService = sceneService;
            Messenger = messenger;
        }

        public override Task Activate()
        {
            //todo: make sure edit service is initialized
            var editService = EditService;
            EditService.ShowNodeEditor();

            _scene = SceneService.GetCurrentScene();
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(_scene);
            adornerLayer.Add(_highlightNodeFrame);

            adornerLayer.Add(_selectionFrame);
            _selectionFrame.IsVisible = false;

            Messenger.Register<NodesSelectedMessage>(this, OnNodesSelected);
            return base.Activate();
        }

        public override void Deactivate()
        {
            Messenger.Unregister<NodesSelectedMessage>(this, OnNodesSelected);
            EditService.HideNodeEditor();
            base.Deactivate();
        }

        private void OnNodesSelected(NodesSelectedMessage obj)
        {
            _highlightNodeFrame.IsVisible = false;
        }

        protected override void OnPointerPressed(object sender, PointerActionEventArgs e)
        {
            _startPos = e.Pointer.ViewportPosition;
            //CapturePointer();

            _selectionFrame.Position = e.Pointer.WorldPosition;
            _selectionFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);

            SelectionService.Select(e.Pointer.WorldPosition, e.KeyModifiers == KeyModifier.Shift);

            if (SelectionService.HasSelectedNodes && SelectionService.Selection.NodesCount == 1)
            {
                if (EditService.FrameEditorNode is FrameEditorNode editor)
                {
                    editor?.ActivateMoveThumb();
                }
            }

            _selectedOnPressed = true;
            _highlightNodeFrame.IsVisible = false;
        }

        protected override void OnPointerReleased(object sender, PointerActionEventArgs e)
        {
            _endPos = e.Pointer.ViewportPosition;

            UpdateDelta();

            _selectionFrame.IsVisible = false;
            _selectionFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);

            //point
            if (_startPos.IsEmpty || _delta.Length <= 2)
            {
                if (!_selectedOnPressed)
                {
                    SelectionService.Select(e.Pointer.WorldPosition, IncrementalSelectionMode);
                }
            }
            else //frame
            {
                SelectionService.Select(_selectionFrame.GetBoundingBox(), IncrementalSelectionMode);
            }

            _startPos = SKPoint.Empty;
            _selectedOnPressed = false;

            //ReleasePointerCapture();
            _startPos = SKPoint.Empty;
            _endPos = SKPoint.Empty;

            base.OnPointerReleased(sender, e);
        }

        private void UpdateDelta()
        {
            _delta = new SKPoint(_endPos.X - _startPos.X, _endPos.Y - _startPos.Y);
        }

        protected override void OnPointerMoved(object sender, PointerActionEventArgs e)
        {
            _endPos = e.Pointer.ViewportPosition;
            var point = e.Pointer.WorldPosition;

            UpdateFrameVisibility(e.Pointer.IsPressed);

            if (_selectionFrame.IsVisible)
                _selectionFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);

            var topNode = _scene
                .GetVisibleDescendants(x => !x.IsInLockedBranch() && x.ContainsPoint(point), false)
                .GetTopNode();

            if (topNode == null || e.Pointer.IsPressed)
            {
                _highlightNodeFrame.IsVisible = false;
                return;
            }

            _highlightNodeFrame.IsVisible = true;
            var bbox = topNode.GetBoundingBox();
            _highlightNodeFrame.Position = bbox.Location;
            _highlightNodeFrame.Size = bbox.Size;
        }

        protected override void OnPointerDoubleClicked(object sender, PointerActionEventArgs e)
        {
            EditService.RequestEdit(SelectionService.Selection.Nodes);
        }

        private void UpdateFrameVisibility(bool isPointerPressed)
        {
            _selectionFrame.IsVisible = isPointerPressed;
        }
    }
}
