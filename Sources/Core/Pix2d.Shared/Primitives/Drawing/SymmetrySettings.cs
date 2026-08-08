using SkiaSharp;

namespace Pix2d.Primitives.Drawing;

/// <summary>
/// How the drawing pipeline reflects every dab. One model covers both the classic Mirror X / Mirror Y
/// toggles and radial symmetry: <see cref="AxisCount"/> mirror axes pass through <see cref="Center"/>,
/// evenly spaced over 180°, the first one rotated <see cref="AngleDegrees"/> away from vertical.
/// <para>N such axes generate the dihedral group D(N) — 2N images of every dab, the original included —
/// so the presets are just points in the same space: <see cref="MirrorX"/> is 1 axis at 0°,
/// <see cref="MirrorY"/> is 1 axis at 90°, and <see cref="MirrorBoth"/> is 2 axes at 0°, i.e. a vertical
/// and a horizontal axis producing four images. (The pre-3.12 "both toggles on" behaviour produced two
/// images — a 180° rotation about the centre — which is D(1) of the diagonal, not what the toggles said.)</para>
/// </summary>
public readonly record struct SymmetrySettings
{
    public const int MinAxisCount = 1;
    public const int MaxAxisCount = 12;

    /// <summary>Symmetry off — what the drawing layer runs with unless the user turns it on.</summary>
    public static readonly SymmetrySettings Off = default;

    private readonly int _axisCount;

    public bool IsEnabled { get; init; }

    /// <summary>
    /// Number of mirror axes through <see cref="Center"/>, evenly spaced over 180°. Clamped on read so a
    /// <c>default</c> value (and anything a slider or a future build could hand in) is always drawable.
    /// </summary>
    public int AxisCount
    {
        get => _axisCount < MinAxisCount ? MinAxisCount : Math.Min(_axisCount, MaxAxisCount);
        init => _axisCount = value;
    }

    /// <summary>Rotation of the first axis away from vertical, in degrees. 0 = vertical (Mirror X).</summary>
    public float AngleDegrees { get; init; }

    /// <summary>
    /// Symmetry centre in canvas pixels, or <c>null</c> for "the middle of the canvas". Null is not the same
    /// as storing the middle: a null centre follows a canvas resize, a moved one is clamped into the new
    /// canvas but keeps where the user put it.
    /// </summary>
    public SKPoint? Center { get; init; }

    public static SymmetrySettings MirrorX(SKPoint? center = null) =>
        new() { IsEnabled = true, AxisCount = 1, AngleDegrees = 0, Center = center };

    public static SymmetrySettings MirrorY(SKPoint? center = null) =>
        new() { IsEnabled = true, AxisCount = 1, AngleDegrees = 90, Center = center };

    public static SymmetrySettings MirrorBoth(SKPoint? center = null) =>
        new() { IsEnabled = true, AxisCount = 2, AngleDegrees = 0, Center = center };

    /// <summary>
    /// The centre in canvas pixels, resolved against the current canvas and clamped into it. Coordinates are
    /// continuous (pixel <c>i</c> spans <c>[i, i+1)</c>), so a whole number sits on a pixel *boundary* — the
    /// default <c>Width / 2</c> is the seam an even-width canvas mirrors about.
    /// </summary>
    public SKPoint GetCenter(SKSize canvas) =>
        Center is { } c
            ? new SKPoint(Math.Clamp(c.X, 0, canvas.Width), Math.Clamp(c.Y, 0, canvas.Height))
            : new SKPoint(canvas.Width * 0.5f, canvas.Height * 0.5f);
}
