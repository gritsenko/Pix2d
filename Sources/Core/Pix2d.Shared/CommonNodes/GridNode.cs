using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

/// <summary>
/// Shared defaults for the canvas grid's appearance (#223). Lives next to <see cref="GridNode"/> so the
/// state layer, the settings UI and the node itself all agree on one baseline value.
/// </summary>
public static class GridDefaults
{
    /// <summary>The grid line color used before the user picks one — the historical neutral gray.</summary>
    public static readonly SKColor Color = new(0xFF909090);

    /// <summary>
    /// Color applied to grid nodes created from now on. <c>SnappingService</c> keeps it in step with the
    /// user's preference so an artboard added *after* the setting was changed doesn't come up gray until
    /// the next grid update — grid nodes are built in <see cref="DrawingContainerBaseNode"/>'s constructor,
    /// which no state watcher can reach.
    /// </summary>
    public static SKColor CurrentColor { get; set; } = Color;

    /// <summary>Reads the persisted "#AARRGGBB" form, falling back to <see cref="Color"/> on missing/garbage input.</summary>
    public static SKColor ParseColor(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SKColor.TryParse(value, out var parsed) ? parsed : Color;

    /// <summary>Writes the persisted "#AARRGGBB" form — SKColor doesn't round-trip through System.Text.Json.</summary>
    public static string FormatColor(SKColor value) => $"#{value.Alpha:X2}{value.Red:X2}{value.Green:X2}{value.Blue:X2}";
}

public class GridNode : SKNode
{
    public SKSize CellSize { get; set; } = new SKSize(8, 8);

    private SKColor _color = GridDefaults.CurrentColor;

    /// <summary>Grid line color, alpha included. Fully transparent draws nothing.</summary>
    public SKColor Color
    {
        get => _color;
        set
        {
            if (_color == value) return;
            _color = value;
            _paintBigCells.Color = value;
            _paintSmallCells.Color = value;
        }
    }

    private SKPaint _paintBigCells = new SKPaint()
    {
        StrokeWidth = 0,
        Color = GridDefaults.CurrentColor,
        IsStroke = true
    };

    private SKPaint _paintSmallCells = new SKPaint()
    {
        StrokeWidth = 0,
        Color = GridDefaults.CurrentColor,
        IsStroke = true
    };

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (_color.Alpha == 0)
            return;

        // LOCAL bounds, not GetBoundingBox(). The renderer has already concatenated this node's chain
        // (adorner layer + own transform) onto the canvas, so the origin here IS the artboard's top-left.
        // GetBoundingBox() returns the same rect in WORLD space, which applied the artboard offset a
        // second time — the grid of an artboard at (800, 0) landed at (1600, 0), and one whose adorner
        // layer had never been re-positioned drew at the scene origin instead of on its own canvas.
        var bounds = LocalBounds;
        var mpx = vp.Zoom < 4 ? CellSize.Width : CellSize.Width;
        var mpy = vp.Zoom < 4 ? CellSize.Height : CellSize.Height;
        RenderGrid(canvas, bounds, mpx, mpy, _paintSmallCells);
        RenderGrid(canvas, bounds, CellSize.Width * mpx, CellSize.Height * mpy, _paintBigCells);
    }

    public void RenderGrid(SKCanvas canvas, SKRect boudns, float stepx, float stepy, SKPaint paint)
    {
        //if (step < 3)
        //    return;

        for (var y = boudns.Top; y < boudns.Bottom; y += stepy)
            canvas.DrawLine(boudns.Left, y, boudns.Right, y, paint);

        for (var x = boudns.Left; x < boudns.Right; x += stepx)
            canvas.DrawLine(x, boudns.Top, x, boudns.Bottom, paint);
    }
}
