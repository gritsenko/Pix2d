using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Operations;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Common.Drawing;
using Pix2d.Plugins.Drawing.Nodes;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

internal sealed record SelectionStateSnapshot(SpriteSelectionNode SelectionLayer, SKBitmap BackgroundBitmap, bool ContourOnly);

internal class InvertSelectionOperation : EditOperationBase, IToolAwareOperation, ISelectionFlowOperation
{
    private readonly DrawingLayerNode _drawingLayer;
    private readonly SelectionStateSnapshot _beforeState;
    private readonly SelectionStateSnapshot? _afterState;

    public string? ToolKeyBeforeOperation { get; }
    public string? ToolKeyAfterOperation { get; }

    public InvertSelectionOperation(
        DrawingLayerNode drawingLayer,
        SelectionStateSnapshot beforeState,
        SelectionStateSnapshot? afterState,
        string? toolKeyBefore,
        string? toolKeyAfter)
    {
        _drawingLayer = drawingLayer;
        _beforeState = beforeState;
        _afterState = afterState;
        ToolKeyBeforeOperation = toolKeyBefore;
        ToolKeyAfterOperation = toolKeyAfter;
    }

    public override void OnPerform()
    {
        ApplyState(_afterState);
    }

    public override void OnPerformUndo()
    {
        ApplyState(_beforeState);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        yield return _drawingLayer;
    }

    private void ApplyState(SelectionStateSnapshot? state)
    {
        if (state == null)
        {
            if (_drawingLayer.HasSelection)
                _drawingLayer.DeactivateSelectionEditor();

            return;
        }

        _drawingLayer.SetSelection(state.SelectionLayer, state.BackgroundBitmap, contourOnly: state.ContourOnly);
    }

    internal static SelectionStateSnapshot? CreateInvertedSelectionState(
        IDrawingTarget drawingTarget,
        SpriteSelectionNode currentSelection,
        SKBitmap sourceBitmap)
    {
        var targetNode = (SKNode)drawingTarget;
        using var currentSelectionMask = BuildSelectionMask(sourceBitmap.Info, targetNode.Position, currentSelection);
        return BuildInverseSelectionState(sourceBitmap, currentSelectionMask, targetNode.Position);
    }

    private static SelectionStateSnapshot? BuildInverseSelectionState(
        SKBitmap sourceBitmap,
        SKBitmap currentSelectionMask,
        SKPoint drawingTargetPosition)
    {
        var width = sourceBitmap.Width;
        var height = sourceBitmap.Height;
        var selectedPoints = new HashSet<SKPointI>();
        var pixelMask = new byte[width * height];

        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;

        var maskSpan = currentSelectionMask.GetPixelSpan();

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var pixelIndex = (x + y * width) * 4;
            if (maskSpan[pixelIndex + 3] > 0)
                continue;

            selectedPoints.Add(new SKPointI(x, y));
            pixelMask[x + y * width] = 1;

            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        if (selectedPoints.Count == 0)
            return null;

        var selectionWidth = right - left + 1;
        var selectionHeight = bottom - top + 1;

        var selectionBitmap = new SKBitmap(new SKImageInfo(selectionWidth, selectionHeight, sourceBitmap.ColorType, sourceBitmap.AlphaType));
        selectionBitmap.Erase(SKColor.Empty);

        var backgroundBitmap = sourceBitmap.Copy();
        var sourceSpan = sourceBitmap.GetPixelSpan();
        var selectionSpan = selectionBitmap.GetPixelSpan();
        var backgroundSpan = backgroundBitmap.GetPixelSpan();

        foreach (var point in selectedPoints)
        {
            var srcIndex = (point.X + point.Y * width) * 4;
            var dstIndex = ((point.X - left) + (point.Y - top) * selectionWidth) * 4;

            selectionSpan[dstIndex] = sourceSpan[srcIndex];
            selectionSpan[dstIndex + 1] = sourceSpan[srcIndex + 1];
            selectionSpan[dstIndex + 2] = sourceSpan[srcIndex + 2];
            selectionSpan[dstIndex + 3] = sourceSpan[srcIndex + 3];

            backgroundSpan[srcIndex] = 0;
            backgroundSpan[srcIndex + 1] = 0;
            backgroundSpan[srcIndex + 2] = 0;
            backgroundSpan[srcIndex + 3] = 0;
        }

        var selectionPath = Algorithms.GetContour(
            selectedPoints,
            pixelMask,
            new SKRectI(0, 0, width - 1, height - 1),
            new SKPointI(0, 0),
            new SKSizeI(width, height),
            out var selectionContours);

        var selectionLayer = new SpriteSelectionNode
        {
            Bitmap = selectionBitmap,
            SelectionPath = selectionPath,
            SelectionContours = selectionContours,
            Opacity = 1,
            Position = new SKPoint(left + drawingTargetPosition.X, top + drawingTargetPosition.Y),
        };

        return new SelectionStateSnapshot(selectionLayer, backgroundBitmap, ContourOnly: true);
    }

    private static SKBitmap BuildSelectionMask(SKImageInfo imageInfo, SKPoint drawingTargetPosition, SpriteSelectionNode selectionLayer)
    {
        var mask = new SKBitmap(imageInfo);
        mask.Erase(SKColor.Empty);

        var selectionBitmap = selectionLayer.Bitmap;
        if (selectionBitmap == null)
            return mask;

        using var canvas = new SKCanvas(mask);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        canvas.Save();
        canvas.Translate(-drawingTargetPosition.X, -drawingTargetPosition.Y);

        if (selectionLayer.SelectionPath != null)
        {
            var pathTransform = CreatePathTransform(selectionLayer, selectionBitmap);
            canvas.Concat(pathTransform);
            canvas.DrawPath(selectionLayer.SelectionPath, paint);
        }
        else
        {
            var bitmapTransform = CreateBitmapTransform(selectionLayer, selectionBitmap);
            canvas.Concat(bitmapTransform);
            canvas.DrawRect(new SKRect(0, 0, selectionBitmap.Width, selectionBitmap.Height), paint);
        }

        canvas.Restore();
        mask.NotifyPixelsChanged();

        return mask;
    }

    private static SKMatrix CreatePathTransform(SpriteSelectionNode selectionLayer, SKBitmap selectionBitmap)
    {
        var translate = SKMatrix.CreateTranslation(
            selectionLayer.Position.X - selectionLayer.PivotPosition.X,
            selectionLayer.Position.Y - selectionLayer.PivotPosition.Y);
        var rotate = SKMatrix.CreateRotationDegrees(
            selectionLayer.Rotation,
            selectionLayer.PivotPosition.X,
            selectionLayer.PivotPosition.Y);
        var scale = SKMatrix.CreateScale(
            selectionLayer.Size.Width / Math.Max(1f, selectionBitmap.Width),
            selectionLayer.Size.Height / Math.Max(1f, selectionBitmap.Height));

        var transform = SKMatrix.Identity;
        SKMatrix.Concat(ref transform, transform, translate);
        SKMatrix.Concat(ref transform, transform, rotate);
        SKMatrix.Concat(ref transform, transform, scale);
        var invertedTranslate = translate.Invert();
        SKMatrix.Concat(ref transform, transform, invertedTranslate);
        return transform;
    }

    private static SKMatrix CreateBitmapTransform(SpriteSelectionNode selectionLayer, SKBitmap selectionBitmap)
    {
        var transform = SKMatrix.CreateTranslation(
            selectionLayer.Position.X - selectionLayer.PivotPosition.X,
            selectionLayer.Position.Y - selectionLayer.PivotPosition.Y);
        var rotate = SKMatrix.CreateRotationDegrees(
            selectionLayer.Rotation,
            selectionLayer.PivotPosition.X,
            selectionLayer.PivotPosition.Y);
        var scale = SKMatrix.CreateScale(
            selectionLayer.Size.Width / Math.Max(1f, selectionBitmap.Width),
            selectionLayer.Size.Height / Math.Max(1f, selectionBitmap.Height));

        SKMatrix.Concat(ref transform, transform, rotate);
        SKMatrix.Concat(ref transform, transform, scale);
        return transform;
    }
}