using System.Threading.Tasks;
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes.Controls;
using Pix2d.InteractiveNodes;
using Pix2d.Messages;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Tools;

[Pix2dTool(
    DisplayName = "Objects tool",
    HotKey = null,
    IconData = "M12.21,98.7v11.97h12.66v12.21H6.1c-3.37,0-6.1-2.73-6.1-6.1V98.7H12.21L12.21,98.7z M27.89,20.54h67.64 c3.37,0,6.1,2.73,6.1,6.1v69.73c0,3.37-2.73,6.1-6.1,6.1H27.89c-3.37,0-6.1-2.73-6.1-6.1V26.64 C21.79,23.27,24.52,20.54,27.89,20.54L27.89,20.54z M91.34,30.82H32.07v61.36h59.27V30.82L91.34,30.82z M0,24.18V6.1 C0,2.73,2.73,0,6.1,0h18.76v12.21H12.21v11.97H0L0,24.18z M110.27,24.18V12.21H97.61V0h18.76c3.37,0,6.1,2.73,6.1,6.1v18.07H110.27 L110.27,24.18z M122.47,98.7v18.07c0,3.37-2.73,6.1-6.1,6.1H97.61v-12.21h12.66V98.7H122.47L122.47,98.7z"
    )]
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

        if (_scene != null)
        {
            var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(_scene);
            adornerLayer.Add(_highlightNodeFrame);

            adornerLayer.Add(_selectionFrame);
            _selectionFrame.IsVisible = false;
        }

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