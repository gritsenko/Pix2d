using SkiaSharp;

namespace Pix2d.Primitives.Drawing;

/// <summary>
/// Turns a <see cref="SymmetrySettings"/> into the concrete extra places a brush dab has to be stamped.
///
/// <para><b>Why the brush footprint, not the anchor, is what gets reflected.</b> A dab's anchor is
/// <c>IPixelBrush.PixelOffset</c> (its <c>CenterPoint</c>) inside a stamp that covers
/// <c>[anchor - CenterPoint, anchor - CenterPoint + Size)</c> — for even sizes that anchor is not the middle
/// of the stamp. Reflecting the anchor alone therefore drifts a wide brush off the axis by half its size.
/// We reflect the footprint's geometric centre in continuous canvas coordinates and convert back, which for
/// an axis-aligned axis reproduces the legacy <c>Width - x - size + 2*offset</c> formula exactly — the old
/// code was this same computation with the centre hard-wired to the middle of the canvas.</para>
///
/// <para>The stamp bitmap itself is never rotated or flipped: reflecting a pixel stamp would need a
/// resample, and every built-in brush is symmetric. A rotated image stamp under radial symmetry is
/// deliberately out of scope.</para>
/// </summary>
public static class SymmetryMath
{
    private const float DegToRad = MathF.PI / 180f;

    /// <summary>
    /// The non-identity elements of the dihedral group the settings describe, as affine maps in canvas
    /// coordinates (the centre translation is baked in). Length is <c>2 * AxisCount - 1</c>, or 0 when
    /// symmetry is off.
    /// </summary>
    public static SKMatrix[] BuildTransforms(SymmetrySettings settings, SKSize canvas)
    {
        if (!settings.IsEnabled)
            return [];

        var n = settings.AxisCount;
        var center = settings.GetCenter(canvas);
        var result = new SKMatrix[2 * n - 1];
        var i = 0;

        for (var k = 0; k < n; k++)
        {
            // Reflection across axis k. Axis direction is measured from vertical, so k = 0 at angle 0 is the
            // vertical axis (Mirror X) and 90° is the horizontal one (Mirror Y).
            var a = (settings.AngleDegrees + k * 180f / n) * DegToRad;
            var dx = MathF.Sin(a);
            var dy = MathF.Cos(a);
            result[i++] = WithCenter(dx * dx - dy * dy, 2 * dx * dy, 2 * dx * dy, dy * dy - dx * dx, center);

            // Composing reflections across two axes 180/n apart is a rotation by 360/n, so the group also
            // contains n-1 rotations. k = 0 would be the identity.
            if (k > 0)
            {
                var r = k * 2f * MathF.PI / n;
                var cos = MathF.Cos(r);
                var sin = MathF.Sin(r);
                result[i++] = WithCenter(cos, -sin, sin, cos, center);
            }
        }

        return result;
    }

    /// <summary>
    /// Every <b>additional</b> anchor a dab at <paramref name="anchor"/> must also be stamped at — the
    /// original is excluded, and duplicates are dropped so a dab sitting on an axis is not stamped twice
    /// (which would double-darken it for any brush below full opacity).
    /// </summary>
    /// <param name="transforms">From <see cref="BuildTransforms"/>; cached by the caller, this runs per dab.</param>
    /// <param name="brushCenter"><c>IPixelBrush.PixelOffset</c> of the brush being stamped.</param>
    /// <param name="brushSize"><c>IPixelBrush.Size</c> — the stamp's side length in pixels.</param>
    public static void GetImageAnchors(
        SKMatrix[] transforms,
        SKPointI anchor,
        SKPointI brushCenter,
        int brushSize,
        List<SKPointI> result)
    {
        result.Clear();
        if (transforms.Length == 0)
            return;

        var half = Math.Max(1, brushSize) * 0.5f;
        var footprintCenter = new SKPoint(
            anchor.X - brushCenter.X + half,
            anchor.Y - brushCenter.Y + half);

        foreach (var m in transforms)
        {
            var p = m.MapPoint(footprintCenter);
            var image = new SKPointI(
                Round(p.X - half) + brushCenter.X,
                Round(p.Y - half) + brushCenter.Y);

            if (image == anchor || result.Contains(image))
                continue;

            result.Add(image);
        }
    }

    /// <summary>
    /// Both endpoints of axis <paramref name="index"/> clipped to the canvas rectangle, for the on-canvas
    /// overlay. Returns false when the axis misses the canvas entirely (possible once the centre is dragged
    /// onto an edge).
    /// </summary>
    public static bool TryGetAxisSegment(SymmetrySettings settings, SKSize canvas, int index, out SKPoint a, out SKPoint b)
    {
        var center = settings.GetCenter(canvas);
        var angle = (settings.AngleDegrees + index * 180f / settings.AxisCount) * DegToRad;
        var d = new SKPoint(MathF.Sin(angle), MathF.Cos(angle));

        // Liang–Barsky against the canvas rect: the axis is infinite, we want the part inside it.
        var tMin = float.NegativeInfinity;
        var tMax = float.PositiveInfinity;

        if (!Clip(-d.X, center.X - 0, ref tMin, ref tMax) ||
            !Clip(d.X, canvas.Width - center.X, ref tMin, ref tMax) ||
            !Clip(-d.Y, center.Y - 0, ref tMin, ref tMax) ||
            !Clip(d.Y, canvas.Height - center.Y, ref tMin, ref tMax))
        {
            a = b = default;
            return false;
        }

        a = new SKPoint(center.X + d.X * tMin, center.Y + d.Y * tMin);
        b = new SKPoint(center.X + d.X * tMax, center.Y + d.Y * tMax);
        return true;
    }

    private static bool Clip(float p, float q, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(p) < 1e-6f)
            return q >= 0; // parallel to this edge: inside iff it starts inside

        var t = q / p;
        if (p < 0)
        {
            if (t > tMax) return false;
            if (t > tMin) tMin = t;
        }
        else
        {
            if (t < tMin) return false;
            if (t < tMax) tMax = t;
        }

        return true;
    }

    // Round-half-up rather than Math.Round's banker's rounding: the axis-aligned cases land on exact
    // integers, so this only decides the ties a rotated axis produces, and it must be consistent.
    private static int Round(float v) => (int)MathF.Floor(v + 0.5f);

    private static SKMatrix WithCenter(float m00, float m01, float m10, float m11, SKPoint c) =>
        new()
        {
            ScaleX = m00,
            SkewX = m01,
            TransX = c.X - (m00 * c.X + m01 * c.Y),
            SkewY = m10,
            ScaleY = m11,
            TransY = c.Y - (m10 * c.X + m11 * c.Y),
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1
        };
}
