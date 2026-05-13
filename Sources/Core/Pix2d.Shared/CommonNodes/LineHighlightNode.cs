using System;
using Pix2d.Abstract.Selection;
using Pix2d.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class LineHighlightNode : SKNode, IDisposable
{
    private SKPoint _offset;
    private SKSize _originalSize;
    private SKPath? Path { get; set; }
    private NodesSelection? TargetSelection { get; set; }

    public LineHighlightNode()
    {
        Path = new SKPath();
        NodeInvalidated += AdjustToTarget;
    }

    public void SetSelection(NodesSelection? targetSelection, SKPath? selectionPath)
    {
        Path = selectionPath;
        if (targetSelection?.Frame != null)
        {
            TargetSelection = targetSelection;
            _offset = targetSelection.Frame!.PivotPosition - targetSelection.Frame!.Position;
            _originalSize = targetSelection.Frame!.Size;
        }
        else
        {
            TargetSelection = null;
        }

        AdjustToTarget(this, EventArgs.Empty);
    }

    private void AdjustToTarget(object? sender, EventArgs e)
    {
        if (TargetSelection?.Frame == null) return;

        var frame = TargetSelection!.Frame!;
        Size = frame.Size;
        Position = frame.Position;
        Rotation = frame.Rotation;
        PivotPosition = frame.PivotPosition - _offset;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (Path == null) return;

        var sx = Size.Width / _originalSize.Width;
        var sy = Size.Height / _originalSize.Height;
        var transformMatrix = SKMatrix.CreateTranslation(_offset.X, _offset.Y)
            .PostConcat(SKMatrix.CreateScale(sx, sy))
            .PostConcat(SKMatrix.CreateTranslation(-_offset.X, -_offset.Y));
        var path = new SKPath();
        Path.Transform(transformMatrix, path);

        // Two-tone marching ants so the contour stays visible on both light and dark canvases.
        var dashLen = vp.PixelsToWorld(4);
        using var blackPaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.Black);
        using var whitePaint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1.5f), SKColors.White);
        blackPaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], 0);
        whitePaint.PathEffect = SKPathEffect.CreateDash([dashLen, dashLen], dashLen);

        canvas.DrawPath(path, blackPaint);
        canvas.DrawPath(path, whitePaint);
    }

    public void Dispose()
    {
        NodeInvalidated -= AdjustToTarget;
    }
}