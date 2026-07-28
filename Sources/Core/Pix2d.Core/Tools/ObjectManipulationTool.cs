#nullable enable
using Pix2d.Abstract.Tools;
using Pix2d.CommonNodes;
using Pix2d.InteractiveNodes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Tools;

/// <summary>
/// Default tool of the General (scene/objects) context — Figma-style object manipulation:
/// <list type="bullet">
/// <item>Click selects the top-most scene object under the cursor; Shift+click toggles it in the selection.</item>
/// <item>Pressing an object starts dragging the whole selection in the same gesture (routed through the
/// <see cref="FrameEditorNode"/> move thumb, so each drag commits one undoable MoveOperation).</item>
/// <item>Pressing empty canvas clears the selection (unless Shift) and drags out a rubber-band that selects
/// every object it touches — except an object the band is entirely inside of, so a marquee dragged inside a
/// big artboard doesn't grab that artboard.</item>
/// <item>Hovering outlines the object under the cursor; double-clicking an artboard dives back into the
/// Sprite edit context for it.</item>
/// </list>
/// Selection state lives in <see cref="ISelectionService"/>; the transform handles around the selection are
/// owned by <see cref="FrameEditorNode"/> (shown by EditService.UpdateEditors whenever the context is not
/// Sprite), so this tool only decides *what* is selected and when a drag starts.
/// </summary>
[Pix2dTool(
    EditContextType = EditContextType.General,
    DisplayName = "Move/select objects",
    HotKey = null,
    IconData = "M13.64,21.97C13.14,22.21 12.54,22 12.31,21.5L10.13,16.76L7.62,18.78C7.45,18.92 7.24,19 7,19A1,1 0 0,1 6,18V3A1,1 0 0,1 7,2C7.24,2 7.47,2.09 7.64,2.23L7.65,2.22L19.14,11.86C19.57,12.22 19.62,12.85 19.27,13.27C19.12,13.45 18.91,13.57 18.7,13.61L15.54,14.23L17.74,18.96C18,19.46 17.76,20.05 17.26,20.28L13.64,21.97Z")]
public class ObjectManipulationTool(
    IEditService editService,
    ISelectionService selectionService,
    ISceneService sceneService,
    IViewPortRefreshService viewPortRefreshService,
    AppState appState) : BaseTool
{
    /// <summary>Viewport-pixel drag distance below which a press+release counts as a click, not a band.</summary>
    private const float ClickDragThresholdPx = 2f;

    private static readonly SKColor AccentColor = new(0xff54a1ea);

    private readonly Frame _hoverHighlightFrame = new() { StrokeColor = AccentColor, StrokeThickness = 2f, IsVisible = false };
    private readonly Frame _rubberBandFrame = new() { StrokeColor = AccentColor, StrokeThickness = 1f, IsVisible = false };

    private bool _rubberBandActive;
    private SKPoint _rubberBandStartWorld;
    private SKPoint _pressViewportPos;
    private SKNode[] _preBandSelection = [];
    private SKNode? _hoveredNode;

    private SKNode? Scene => sceneService.GetCurrentScene();

    public override Task Activate()
    {
        editService.ShowNodeEditor();
        return base.Activate();
    }

    public override void Deactivate()
    {
        _rubberBandActive = false;
        SetHoveredNode(null);
        _hoverHighlightFrame.RemoveFromParent();
        _rubberBandFrame.RemoveFromParent();
        _rubberBandFrame.IsVisible = false;

        editService.HideNodeEditor();
        viewPortRefreshService.Refresh();
        base.Deactivate();
    }

    protected override void OnPointerPressed(object? sender, PointerActionEventArgs e)
    {
        var scene = Scene;
        if (scene == null)
            return;

        EnsureOverlays(scene);
        SetHoveredNode(null);

        var worldPos = e.Pointer.WorldPosition;
        _pressViewportPos = e.Pointer.ViewportPosition;
        var shift = e.KeyModifiers.HasFlag(KeyModifier.Shift);
        var hit = HitTest(scene, worldPos);

        if (hit != null)
        {
            var selected = selectionService.Selection?.Nodes ?? [];
            if (shift)
            {
                var newSet = selected.ToList();
                if (!newSet.Remove(hit))
                    newSet.Add(hit);
                selectionService.Select(newSet.ToArray());
            }
            else
            {
                if (!selected.Contains(hit))
                    selectionService.Select(hit);

                // Select() has already re-targeted the FrameEditorNode synchronously (NodesSelectedMessage →
                // EditService.UpdateEditors), so forwarding this press to its move thumb starts a drag
                // session at once — select-and-drag in one gesture, one undoable MoveOperation on release.
                if (appState.CurrentProject.FrameEditorNode is FrameEditorNode { IsVisible: true } frameEditor)
                    frameEditor.ActivateMoveThumb();
            }
        }
        else
        {
            if (!shift)
                selectionService.ClearSelection();

            // Band always merges with what was selected at press time: empty for a plain drag
            // (just cleared), the existing selection for a Shift-drag.
            _preBandSelection = selectionService.Selection?.Nodes ?? [];
            _rubberBandActive = true;
            _rubberBandStartWorld = worldPos;
            _rubberBandFrame.Position = worldPos;
            _rubberBandFrame.SetSecondCornerPosition(worldPos);
            _rubberBandFrame.IsVisible = true;
        }

        viewPortRefreshService.Refresh();
    }

    protected override void OnPointerMoved(object? sender, PointerActionEventArgs e)
    {
        var scene = Scene;
        if (scene == null)
            return;

        if (_rubberBandActive)
        {
            if (e.Pointer.IsPressed)
            {
                _rubberBandFrame.SetSecondCornerPosition(e.Pointer.WorldPosition);
                viewPortRefreshService.Refresh();
                return;
            }

            // The release happened outside the viewport — drop the band without selecting.
            _rubberBandActive = false;
            _rubberBandFrame.IsVisible = false;
            viewPortRefreshService.Refresh();
        }

        if (e.Pointer.IsPressed)
            return; // pressed moves belong to a thumb drag, not hover

        EnsureOverlays(scene);

        var hit = HitTest(scene, e.Pointer.WorldPosition);
        if (hit != null && selectionService.Selection?.Nodes.Contains(hit) == true)
            hit = null; // the selection frame already outlines it
        SetHoveredNode(hit);
    }

    protected override void OnPointerReleased(object? sender, PointerActionEventArgs e)
    {
        if (!_rubberBandActive)
            return;

        _rubberBandActive = false;
        _rubberBandFrame.IsVisible = false;

        var scene = Scene;
        if (scene != null)
        {
            var viewportDelta = e.Pointer.ViewportPosition - _pressViewportPos;
            if (viewportDelta.Length > ClickDragThresholdPx)
            {
                var end = e.Pointer.WorldPosition;
                var rect = new SKRect(_rubberBandStartWorld.X, _rubberBandStartWorld.Y, end.X, end.Y).Standardized;

                var hits = scene.Nodes.Where(x =>
                    x.IsVisible && !x.IsAdorner && !x.IsInLockedBranch() &&
                    x.GetBoundingBox().IntersectsWith(rect) &&
                    !x.GetBoundingBox().Contains(rect));

                var result = _preBandSelection.Concat(hits).Distinct().ToArray();
                if (result.Length > 0)
                    selectionService.Select(result);
                else
                    selectionService.ClearSelection();
            }
        }

        viewPortRefreshService.Refresh();
    }

    protected override void OnPointerDoubleClicked(object? sender, PointerActionEventArgs e)
    {
        var scene = Scene;
        if (scene == null)
            return;

        // Figma-style "enter the object": double-clicking an artboard's content switches back to the
        // Sprite edit context for it (RequestEdit sets CurrentContextType, which also re-activates the
        // default Sprite tool and deactivates this one).
        if (HitTest(scene, e.Pointer.WorldPosition) is Pix2dSprite sprite)
        {
            SetHoveredNode(null);
            editService.RequestEdit([sprite]);
        }
    }

    /// <summary>Top-most direct scene child (artboard-level object) under the point. Deliberately not a deep
    /// hit-test: in the General context manipulation happens at object level, never inside layers.</summary>
    private static SKNode? HitTest(SKNode scene, SKPoint worldPos)
        => scene.Nodes.LastOrDefault(x =>
            x.IsVisible && !x.IsAdorner && !x.IsInLockedBranch() &&
            x.GetBoundingBox().Contains(worldPos));

    private void SetHoveredNode(SKNode? node)
    {
        if (_hoveredNode == node)
            return;

        _hoveredNode = node;
        if (node == null)
        {
            _hoverHighlightFrame.IsVisible = false;
        }
        else
        {
            var bbox = node.GetBoundingBox();
            _hoverHighlightFrame.Position = bbox.Location;
            _hoverHighlightFrame.Size = bbox.Size;
            _hoverHighlightFrame.IsVisible = true;
        }

        viewPortRefreshService.Refresh();
    }

    /// <summary>Keeps the overlay frames parented to the *current* scene's adorner layer — the tool instance
    /// survives project-tab switches (ActivateTool early-returns on the same key), so they are re-attached
    /// lazily instead of once at Activate.</summary>
    private void EnsureOverlays(SKNode scene)
    {
        var adornerLayer = SkiaNodes.AdornerLayer.GetAdornerLayer(scene);
        if (adornerLayer == null)
            return;

        AttachTo(adornerLayer, _hoverHighlightFrame);
        AttachTo(adornerLayer, _rubberBandFrame);
    }

    private static void AttachTo(SkiaNodes.AdornerLayer layer, SKNode node)
    {
        if (node.Parent == layer)
            return;

        node.RemoveFromParent();
        layer.Add(node);
    }
}
