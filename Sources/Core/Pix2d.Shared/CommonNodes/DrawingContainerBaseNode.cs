using Newtonsoft.Json;
using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class DrawingContainerBaseNode : SKNode, IContainerNode, IClippingSource
{
    private GridNode _grid;
    private bool _showGrid;

    public SKNodeClipMode ClipMode => SKNodeClipMode.Rect;
    public SKRect ClipBounds => LocalBounds;

    public SKSize GridCellSize
    {
        get => _grid.CellSize;
        set => _grid.CellSize = value;
    }

    /// <summary>Grid line color (alpha included) — the user preference from <c>AppState.GridColor</c> (#223).</summary>
    // Not document state: it's a per-user readability preference persisted in AppSettings and pushed into every
    // container by SnappingService, so persisting it would carry one machine's preference into everyone's files.
    [JsonIgnore]
    public SKColor GridColor
    {
        get => _grid.Color;
        set => _grid.Color = value;
    }

    public bool ShowGrid
    {
        get => _showGrid;
        set
        {
            _showGrid = value;
            _grid.IsVisible = value;
        }
    }

    public SKColor BackgroundColor { get; set; } = SKColors.White;
    public bool UseBackgroundColor { get; set; }

    public DrawingContainerBaseNode()
    {
        _grid = new GridNode
        {
            Size = this.Size
        };
        var adorner = AdornerLayer.GetAdornerLayer(this);
        adorner.Nodes.Add(_grid);
        _grid.IsVisible = _showGrid;
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        _grid.Size = Size;
    }

    /// <summary>
    /// An adorner layer is painted in its owner's PARENT space, with the layer's own <see cref="SKNode.Position"/>
    /// as the offset onto the owner. <see cref="AdornerLayer.GetAdornerLayer"/> stamps that position only when it
    /// is called — and this class calls it from its constructor, before the node has a position or a parent — so
    /// the layer would keep the (0,0) it was born with. Only the *active* drawing target got a corrected value,
    /// as a side effect of `DrawingService` re-fetching the layer, and even that went stale as soon as the
    /// artboard moved. Result: every other artboard's grid piled up at the scene origin. Re-syncing per frame is
    /// cheap (the transform is cached and the assignment is guarded), and it keeps the layer correct through
    /// moves, arranges and undo without needing a hook on every one of them.
    /// </summary>
    private void SyncAdornerLayerPosition()
    {
        if (AdornerLayer is not { } layer)
            return;

        var position = GetGlobalPosition();
        if (layer.Position != position)
            layer.Position = position;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        SyncAdornerLayerPosition();

        if (vp.Settings.RenderAdorners && !UseBackgroundColor)
        {
            CanvasCheckerboard.Draw(canvas, vp, LocalBounds);
        }
        else if (UseBackgroundColor && BackgroundColor != default)
        {
            using var paint = canvas.GetSolidFillPaint(BackgroundColor);
            canvas.DrawRect(LocalBounds, paint);
        }
    }

    public virtual void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
    {
        throw new NotImplementedException();
    }

    public virtual void Crop(SKRect targetBounds)
    {
        throw new NotImplementedException();
    }
}