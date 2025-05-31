using SkiaNodes;
using SkiaNodes.Abstract;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class DrawingContainerBaseNode : SKNode, IContainerNode, IClippingSource
{
    private static readonly SKColor Dark = new(0xffd2d2d2);
    private static readonly SKColor Bright = new(0xffffffff);
    private static readonly SKBitmap CheckerPattern = new(2, 2, Pix2DAppSettings.ColorType, SKAlphaType.Premul)
    {
        Pixels = [Bright, Dark, Dark, Bright]
    };

    static DrawingContainerBaseNode()
    {
    }

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
            DrawDynamicCheckerboard(canvas, vp);
        }
        else if (UseBackgroundColor && BackgroundColor != default)
        {
            using var paint = canvas.GetSolidFillPaint(BackgroundColor);
            canvas.DrawRect(LocalBounds, paint);
        }
    }

    private void DrawDynamicCheckerboard(SKCanvas canvas, ViewPort vp)
    {
        var cellSize = GridCellSize.Width < 1 ? 8 : GridCellSize.Width;
        var effectiveCellSize = vp.Zoom < 4 ? cellSize : 1;

        using var paint = new SKPaint
        {
            Shader = SKShader.CreateBitmap(
                CheckerPattern,
                SKShaderTileMode.Repeat,
                SKShaderTileMode.Repeat,
                SKMatrix.CreateScale(effectiveCellSize, effectiveCellSize)
            ),
            FilterQuality = SKFilterQuality.None // Critical for crisp pixels
        };

        canvas.DrawRect(LocalBounds, paint);
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