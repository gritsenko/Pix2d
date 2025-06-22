using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.InteractiveNodes.Thumbs;

public class NodeManipulateThumbBase : ThumbNode
{

    private NodesSelection _selection;

    public bool SnapToPixels { get; set; }

    public NodesSelection TargetSelection
    {
        get => _selection;
        set
        {
            if (_selection != value)
            {

                if (_selection != null)
                    _selection.Invalidated -= SelectionOnInvalidated;

                _selection = value;

                if (_selection != null)
                    _selection.Invalidated += SelectionOnInvalidated;

                AdjustDimensionsToTargets(_selection);
            }

        }
    }

    protected virtual void AdjustDimensionsToTargets(NodesSelection selection)
    {

        var bounds = selection.Bounds;
        Position = bounds.Location;
        Size = bounds.Size;
    }

    protected void DragNode(SKNode node, SKPoint initialGlobalPos, SKPoint delta, bool snapToPixels)
    {
        if (snapToPixels)
        {
            node.SetGlobalPosition(initialGlobalPos + delta.Floor());
        }
        else
        {
            node.SetGlobalPosition(initialGlobalPos + delta);
        }
    }

    private void SelectionOnInvalidated(object sender, EventArgs e)
    {
        AdjustDimensionsToTargets(_selection);
    }

    public void UpdateToTargets()
    {
        AdjustDimensionsToTargets(_selection);
    }

    protected override SKColor BBoxColor => _bboxColor ??= SKColors.LawnGreen;

    protected override void DrawDebugStuff(SKCanvas canvas, ViewPort vp)
    {
        canvas.Save();
        var transform = vp.ResultTransformMatrix;
        canvas.SetMatrix(transform);

        DrawBoundingBox(canvas, vp, 2, BBoxColor);

        using var paint = new SKPaint();
        paint.Color = BBoxColor;
        paint.TextSize = 2;


        var pos = GetGlobalPosition();

        canvas.DrawText($"{this.Name}[{this.GetType().Name}]", pos.X, pos.Y, paint);

        DrawHitZone(canvas, vp, 2, SKColors.Red);

        canvas.Restore();
    }

}