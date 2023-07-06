using System;
using Pix2d.Abstract.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class LineHighlightNode : SKNode, IDisposable
{
    private SKPoint _offset;
    private SKSize _originalSize;
    private SKPath Path { get; set; }
    private NodesSelection TargetSelection { get; set; }

    public LineHighlightNode()
    {
        NodeInvalidated += AdjustToTarget;
    }

    public void SetSelection(NodesSelection targetSelection, SKPath selectionPath)
    {
        TargetSelection = targetSelection;
        Path = selectionPath;
        _offset = TargetSelection.Frame.PivotPosition - TargetSelection.Frame.Position;
        _originalSize = TargetSelection.Frame.Size;
    }

    private void AdjustToTarget(object sender, EventArgs e)
    {
        var frame = TargetSelection.Frame;
        Size = frame.Size;
        Position = frame.Position;
        Rotation = frame.Rotation;
        PivotPosition = frame.PivotPosition - _offset;
    }

    public override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (Path == null) return;
        
        using var paint = canvas.GetSimpleStrokePaint(vp.PixelsToWorld(1), SKColors.Black);
        paint.PathEffect = SKPathEffect.CreateDash(new float[] {vp.PixelsToWorld(2), vp.PixelsToWorld(2)}, 0);

        var sx = Size.Width / _originalSize.Width;
        var sy = Size.Height / _originalSize.Height;
        var transformMatrix = SKMatrix.CreateTranslation(_offset.X, _offset.Y)
            .PostConcat(SKMatrix.CreateScale(sx, sy))
            .PostConcat(SKMatrix.CreateTranslation(-_offset.X, -_offset.Y));
        var path = new SKPath();
        Path.Transform(transformMatrix, path);
        
        canvas.DrawPath(path, paint);
    }

    public void Dispose()
    {
        NodeInvalidated -= AdjustToTarget;
    }
}