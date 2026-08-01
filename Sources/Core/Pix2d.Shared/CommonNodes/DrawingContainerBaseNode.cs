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

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
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