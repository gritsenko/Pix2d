using Pix2d.Plugins.Drawing.Nodes;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Common.Drawing;

/// <summary>
/// A selection region rebuilt from a mask: the selection layer (pixels + contour) plus the background
/// the layer was lifted out of. Same pair <c>SelectionController</c> keeps live and
/// <c>BeginSelectionOperation</c> replays.
/// </summary>
public sealed record SelectionRegion(SpriteSelectionNode SelectionLayer, SKBitmap BackgroundBitmap);

/// <summary>
/// Set algebra on pixel selections, in drawing-target pixel space.
///
/// <para>A selection has two representations in this codebase: the live one — a
/// <see cref="SpriteSelectionNode"/> carrying lifted pixels, a contour path and its own transform — and a
/// flat canvas-sized <c>byte[]</c> mask (1 = selected, indexed <c>x + y * width</c>). Only the mask form
/// can be combined, so anything that unions / subtracts / intersects selections goes
/// <see cref="Rasterize"/> → <see cref="Combine"/> → <see cref="BuildRegion"/>.</para>
///
/// <para>Used by the Shift/Ctrl marquee combining in <c>SelectionController</c> and by
/// <c>InvertSelectionOperation</c>, which is the same round trip with the mask negated.</para>
/// </summary>
public static class SelectionMaskOps
{
    /// <summary>
    /// Flattens a live selection layer into a canvas-space mask, honouring its current transform — this is
    /// what keeps a marquee the user has already dragged around from combining against its original spot.
    /// Rasterizes the contour path when there is one and the layer's rectangle otherwise (rect marquee /
    /// select-all / paste, which carry no path), matching how the marching ants are drawn.
    /// </summary>
    public static byte[] Rasterize(SpriteSelectionNode selectionLayer, SKPoint drawingTargetPosition, int width, int height)
    {
        var mask = new byte[Math.Max(0, width) * Math.Max(0, height)];

        var selectionBitmap = selectionLayer.Bitmap;
        if (selectionBitmap == null || width <= 0 || height <= 0)
            return mask;

        using var maskBitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        maskBitmap.Erase(SKColor.Empty);

        using (var canvas = new SKCanvas(maskBitmap))
        using (var paint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Fill, IsAntialias = false })
        {
            canvas.Translate(-drawingTargetPosition.X, -drawingTargetPosition.Y);

            if (selectionLayer.SelectionPath is { } contourPath)
            {
                // SelectionPath is stored in the *canvas* coordinates it was traced in, while the layer's
                // transform maps from selection-local space — so the path has to be normalized to its own
                // origin first, exactly as LineHighlightNode does before rendering the marching ants.
                // Without this the region lands at twice its offset, and everything downstream (invert,
                // Shift/Ctrl combining) silently operates on the wrong pixels.
                var origin = GetContourBounds(contourPath, selectionLayer.SelectionContours);
                using var localPath = new SKPath();
                contourPath.Transform(SKMatrix.CreateTranslation(-origin.Left, -origin.Top), localPath);

                // Even-odd, not winding: a traced contour set carries holes (invert a blob and the blob
                // becomes a hole in the complement) as separate sub-contours, and Algorithms.GetContour
                // makes no promise about their winding direction — under the default winding rule a hole
                // wound the same way as its outer contour fills solid.
                localPath.FillType = SKPathFillType.EvenOdd;

                canvas.Concat(CreateLayerTransform(selectionLayer, origin.Width, origin.Height));
                canvas.DrawPath(localPath, paint);
            }
            else
            {
                canvas.Concat(CreateLayerTransform(selectionLayer, selectionBitmap.Width, selectionBitmap.Height));
                canvas.DrawRect(new SKRect(0, 0, selectionBitmap.Width, selectionBitmap.Height), paint);
            }
        }

        var span = maskBitmap.GetPixelSpan();
        for (var i = 0; i < mask.Length; i++)
            mask[i] = span[i * 4 + 3] > 0 ? (byte)1 : (byte)0;

        return mask;
    }

    /// <summary>
    /// Applies <paramref name="mode"/> to a base mask and the mask of the marquee just drawn, returning a
    /// fresh array. A null <paramref name="addend"/> means the new gesture selected nothing at all, which
    /// leaves Add as a no-op and collapses Subtract/Intersect the way the set algebra says it should.
    /// </summary>
    public static byte[] Combine(byte[] baseMask, byte[]? addend, SelectionCombineMode mode)
    {
        if (mode == SelectionCombineMode.Replace)
            return addend ?? new byte[baseMask.Length];

        var result = new byte[baseMask.Length];
        if (addend == null || addend.Length != baseMask.Length)
        {
            // Nothing new to combine with: adding or subtracting an empty region leaves the base alone,
            // intersecting with it leaves nothing.
            if (mode != SelectionCombineMode.Intersect)
                Array.Copy(baseMask, result, baseMask.Length);
            return result;
        }

        for (var i = 0; i < result.Length; i++)
        {
            var inBase = baseMask[i] > 0;
            var inNew = addend[i] > 0;
            var selected = mode switch
            {
                SelectionCombineMode.Add => inBase || inNew,
                SelectionCombineMode.Subtract => inBase && !inNew,
                SelectionCombineMode.Intersect => inBase && inNew,
                _ => inNew
            };

            if (selected)
                result[i] = 1;
        }

        return result;
    }

    public static void InvertInPlace(byte[] mask)
    {
        for (var i = 0; i < mask.Length; i++)
            mask[i] = mask[i] > 0 ? (byte)0 : (byte)1;
    }

    public static bool IsEmpty(byte[] mask)
    {
        foreach (var v in mask)
            if (v > 0)
                return false;

        return true;
    }

    /// <summary>
    /// Traces the marching-ants outline of a mask, in drawing-target pixel coordinates. Returns null for an
    /// empty mask.
    /// </summary>
    public static SKPath? BuildContour(byte[] mask, int width, int height, out List<List<SKPoint>>? contours)
    {
        contours = null;
        if (width <= 0 || height <= 0 || mask.Length < width * height)
            return null;

        var points = CollectPoints(mask, width, height);
        if (points.Count == 0)
            return null;

        var path = Algorithms.GetContour(
            points,
            mask,
            new SKRectI(0, 0, width - 1, height - 1),
            new SKPointI(0, 0),
            new SKSizeI(width, height),
            out var traced);

        contours = traced;
        return path;
    }

    /// <summary>
    /// Rebuilds the live selection pair from a mask: a <see cref="SpriteSelectionNode"/> cropped to the
    /// mask's bounding box holding the selected pixels of <paramref name="sourceBitmap"/>, plus a copy of
    /// the source with those pixels cleared (the background the editor shows underneath a lifted
    /// selection). Returns null when the mask selects nothing.
    /// </summary>
    public static SelectionRegion? BuildRegion(SKBitmap sourceBitmap, byte[] mask, SKPoint drawingTargetPosition)
    {
        var width = sourceBitmap.Width;
        var height = sourceBitmap.Height;
        if (width <= 0 || height <= 0 || mask.Length < width * height)
            return null;

        var selectedPoints = CollectPoints(mask, width, height);
        if (selectedPoints.Count == 0)
            return null;

        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        foreach (var p in selectedPoints)
        {
            left = Math.Min(left, p.X);
            top = Math.Min(top, p.Y);
            right = Math.Max(right, p.X);
            bottom = Math.Max(bottom, p.Y);
        }

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
            var dstIndex = (point.X - left + (point.Y - top) * selectionWidth) * 4;

            selectionSpan[dstIndex] = sourceSpan[srcIndex];
            selectionSpan[dstIndex + 1] = sourceSpan[srcIndex + 1];
            selectionSpan[dstIndex + 2] = sourceSpan[srcIndex + 2];
            selectionSpan[dstIndex + 3] = sourceSpan[srcIndex + 3];

            backgroundSpan[srcIndex] = 0;
            backgroundSpan[srcIndex + 1] = 0;
            backgroundSpan[srcIndex + 2] = 0;
            backgroundSpan[srcIndex + 3] = 0;
        }

        // Contour is traced over the full-canvas mask (not the cropped one) so the path lands in the same
        // coordinate space the selectors produce, i.e. drawing-target pixels.
        var selectionPath = Algorithms.GetContour(
            selectedPoints,
            mask,
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

        return new SelectionRegion(selectionLayer, backgroundBitmap);
    }

    private static HashSet<SKPointI> CollectPoints(byte[] mask, int width, int height)
    {
        var points = new HashSet<SKPointI>();
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                if (mask[x + y * width] > 0)
                    points.Add(new SKPointI(x, y));

        return points;
    }

    /// <summary>
    /// Selection-local space → world: the node's own local transform (<c>SKNode.Invalidate</c>) plus the
    /// scale the frame has been resized by, where <paramref name="sourceWidth"/>/<paramref name="sourceHeight"/>
    /// are the unscaled extents of whatever is being drawn (the lifted bitmap, or the contour's bounds).
    /// </summary>
    private static SKMatrix CreateLayerTransform(SpriteSelectionNode selectionLayer, float sourceWidth, float sourceHeight)
    {
        var transform = SKMatrix.CreateTranslation(
            selectionLayer.Position.X - selectionLayer.PivotPosition.X,
            selectionLayer.Position.Y - selectionLayer.PivotPosition.Y);
        var rotate = SKMatrix.CreateRotationDegrees(
            selectionLayer.Rotation,
            selectionLayer.PivotPosition.X,
            selectionLayer.PivotPosition.Y);
        var scale = SKMatrix.CreateScale(
            selectionLayer.Size.Width / Math.Max(1f, sourceWidth),
            selectionLayer.Size.Height / Math.Max(1f, sourceHeight));

        SKMatrix.Concat(ref transform, transform, rotate);
        SKMatrix.Concat(ref transform, transform, scale);
        return transform;
    }

    /// <summary>
    /// Bounds of the traced contour, preferring the raw vertex lists over <see cref="SKPath.Bounds"/> —
    /// same choice <c>LineHighlightNode.GetPathBounds</c> makes, and the two must agree or the mask and the
    /// marching ants describe different regions.
    /// </summary>
    private static SKRect GetContourBounds(SKPath path, IReadOnlyList<IReadOnlyList<SKPoint>>? contours)
    {
        if (contours == null || contours.Count == 0)
            return path.Bounds;

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        foreach (var contour in contours)
            foreach (var point in contour)
            {
                minX = MathF.Min(minX, point.X);
                minY = MathF.Min(minY, point.Y);
                maxX = MathF.Max(maxX, point.X);
                maxY = MathF.Max(maxY, point.Y);
            }

        if (float.IsInfinity(minX) || float.IsInfinity(minY) || float.IsInfinity(maxX) || float.IsInfinity(maxY))
            return path.Bounds;

        return new SKRect(minX, minY, maxX, maxY);
    }
}
