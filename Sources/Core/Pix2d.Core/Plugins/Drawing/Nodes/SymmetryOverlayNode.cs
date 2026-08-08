#nullable enable
using Pix2d.Primitives.Drawing;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

/// <summary>
/// The on-canvas symmetry axes and the handle that moves them. Lives as a child of
/// <see cref="DrawingLayerNode"/>, so it shares the drawing layer's coordinate space exactly — the axes are
/// computed in canvas pixels by <see cref="SymmetryMath"/> and drawn without any further mapping.
///
/// <para><b>The one hit target is a grip parked just outside the canvas</b>, where the first axis leaves it —
/// like the handle on a ruler guide. Two things it must not be: the axis lines themselves (they run straight
/// through the area being drawn, so grabbable lines would silently eat strokes) and a disc at the
/// intersection (which defaults to the middle of the canvas — the worst possible place to put a 13px dead
/// zone on a 32px sprite; a headless regression check caught exactly that, painting 0 pixels instead of 2).
/// Drag the grip to move the axes, double-click it to put them back in the middle.</para>
/// </summary>
internal sealed class SymmetryOverlayNode : SKNode
{
    private const float AxisStrokePixels = 1.25f;
    private const float AxisDashPixels = 6f;
    private const float HandleRadiusPixels = 6f;
    // Extra grab room around the drawn grip. A finger is not a mouse, and the grip is deliberately small.
    private const float HandleGrabSlopPixels = 7f;
    // How far past the canvas edge the grip is parked, measured along the axis from where it exits.
    private const float HandleOffsetPixels = 12f;
    private const float CenterMarkerPixels = 3.5f;

    private static readonly SKColor AxisColor = new(0x35, 0xC8, 0xFF);
    private static readonly SKColor ShadowColor = new(0x00, 0x00, 0x00, 0xB0);

    private SymmetrySettings _settings = SymmetrySettings.Off;
    private SKPoint _grabOffset;
    private bool _isDragging;
    private bool _isHovered;

    // World units per screen pixel, cached from the last paint. ContainsPoint gets no ViewPort, and the
    // handle's grab radius is defined in screen pixels — without this the hit zone would shrink with zoom
    // exactly where it matters (a 64px canvas at 8x zoom).
    private float _worldPerPixel = 1f;

    public SymmetryOverlayNode()
    {
        IsInteractive = true;
        IsVisible = false;
    }

    /// <summary>Raised with the new centre in canvas pixels, or null to mean "back to the canvas centre".</summary>
    public Action<SKPoint?>? CenterChanged { get; set; }

    public SymmetrySettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            IsVisible = value.IsEnabled;
        }
    }

    public override bool ContainsPoint(SKPoint worldPos)
    {
        if (!_settings.IsEnabled)
            return false;

        var local = GetLocalPosition(worldPos);
        var grip = GetGripPosition();
        var radius = (HandleRadiusPixels + HandleGrabSlopPixels) * _worldPerPixel;
        var dx = local.X - grip.X;
        var dy = local.Y - grip.Y;
        return dx * dx + dy * dy <= radius * radius;
    }

    /// <summary>
    /// A grab hand over the grip — the only affordance saying the axes can be moved at all, since the grip
    /// sits on empty canvas background where nothing else suggests it is draggable. Also held for the whole
    /// drag, where <see cref="ContainsPoint"/> is false as soon as the pointer runs ahead of the grip.
    /// </summary>
    public override SKCursorType GetHoverCursor(SKPoint worldPos) =>
        _isDragging || ContainsPoint(worldPos) ? SKCursorType.Hand : SKCursorType.Default;

    /// <summary>
    /// Where the grip sits: the upper end of the first axis, pushed a little further out so it clears the
    /// canvas. It therefore travels with the axes and always reads as attached to them, whatever the angle.
    /// </summary>
    private SKPoint GetGripPosition()
    {
        var center = _settings.GetCenter(Size);

        if (!SymmetryMath.TryGetAxisSegment(_settings, Size, 0, out var a, out var b))
            return new SKPoint(center.X, center.Y - HandleOffsetPixels * _worldPerPixel);

        // "Upper" end, with X breaking the tie so a horizontal axis picks its left end deterministically
        // rather than flipping sides as the centre is dragged.
        var end = a.Y < b.Y || (a.Y == b.Y && a.X <= b.X) ? a : b;

        var dx = end.X - center.X;
        var dy = end.Y - center.Y;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1e-4f)
            return new SKPoint(center.X, center.Y - HandleOffsetPixels * _worldPerPixel);

        var offset = HandleOffsetPixels * _worldPerPixel / len;
        return new SKPoint(end.X + dx * offset, end.Y + dy * offset);
    }

    public override void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount)
    {
        base.OnPointerPressed(eventArgs, clickCount);

        if (!_settings.IsEnabled)
            return;

        if (clickCount > 1)
        {
            // Double-click the grip = reset. The same reset lives in the settings popup; this is the
            // one that is reachable without leaving the canvas.
            CenterChanged?.Invoke(null);
            eventArgs.Handled = true;
            return;
        }

        var local = GetLocalPosition(eventArgs.Pointer.WorldPosition);
        var center = _settings.GetCenter(Size);
        // Offset from the pointer to the CENTRE, not to the grip: the grip's own position is derived from
        // the centre, so tracking it directly would chase a moving target.
        _grabOffset = new SKPoint(center.X - local.X, center.Y - local.Y);
        _isDragging = true;
        CapturePointer();
        eventArgs.Handled = true;
    }

    public override void OnPointerMoved(PointerActionEventArgs eventArgs)
    {
        base.OnPointerMoved(eventArgs);

        if (!_isDragging || SKInput.Current.CapturedPointerBy != this)
            return;

        var local = GetLocalPosition(eventArgs.Pointer.WorldPosition);
        CenterChanged?.Invoke(Snap(new SKPoint(local.X + _grabOffset.X, local.Y + _grabOffset.Y)));
        eventArgs.Handled = true;
    }

    public override void OnPointerReleased(PointerActionEventArgs eventArgs)
    {
        base.OnPointerReleased(eventArgs);

        if (_isDragging)
            eventArgs.Handled = true;

        _isDragging = false;
        ReleasePointerCapture();
    }

    public override void OnPointerEnter(SKPoint pos)
    {
        _isHovered = true;
        base.OnPointerEnter(pos);
    }

    public override void OnPointerLeave(SKPoint pos)
    {
        _isHovered = false;
        base.OnPointerLeave(pos);
    }

    /// <summary>
    /// Half-pixel steps, clamped to the canvas. Whole numbers are pixel <i>boundaries</i>, so the halves are
    /// what lets an axis sit on the middle of a pixel column — the only way an odd-width canvas can be
    /// symmetric about a real pixel rather than a seam.
    /// </summary>
    private SKPoint Snap(SKPoint p) =>
        new(
            Math.Clamp(MathF.Round(p.X * 2f) * 0.5f, 0, Size.Width),
            Math.Clamp(MathF.Round(p.Y * 2f) * 0.5f, 0, Size.Height));

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (!_settings.IsEnabled || Size.Width <= 0 || Size.Height <= 0)
            return;

        _worldPerPixel = vp.PixelsToWorld(1f);

        var stroke = AxisStrokePixels * _worldPerPixel;
        var dash = AxisDashPixels * _worldPerPixel;

        // Path effects don't transfer ownership when assigned to a paint, and OnDraw runs every frame.
        using var shadowPaint = canvas.GetSimpleStrokePaint(stroke * 2.2f, ShadowColor);
        using var axisPaint = canvas.GetSimpleStrokePaint(stroke, AxisColor);
        using var dashEffect = SKPathEffect.CreateDash([dash, dash], 0);
        axisPaint.PathEffect = dashEffect;

        for (var i = 0; i < _settings.AxisCount; i++)
        {
            if (!SymmetryMath.TryGetAxisSegment(_settings, Size, i, out var a, out var b))
                continue;

            canvas.DrawLine(a, b, shadowPaint);
            canvas.DrawLine(a, b, axisPaint);
        }

        DrawCenterMarker(canvas, _settings.GetCenter(Size));
        DrawGrip(canvas, GetGripPosition());
    }

    // A small cross on the intersection. Purely informational — it is not a hit target, so it can sit in
    // the middle of the artwork without costing the user a stroke.
    private void DrawCenterMarker(SKCanvas canvas, SKPoint center)
    {
        var arm = CenterMarkerPixels * _worldPerPixel;
        using var shadow = canvas.GetSimpleStrokePaint(2.4f * _worldPerPixel, ShadowColor);
        using var pen = canvas.GetSimpleStrokePaint(1.2f * _worldPerPixel, AxisColor);

        foreach (var paint in new[] { shadow, pen })
        {
            canvas.DrawLine(center.X - arm, center.Y, center.X + arm, center.Y, paint);
            canvas.DrawLine(center.X, center.Y - arm, center.X, center.Y + arm, paint);
        }
    }

    private void DrawGrip(SKCanvas canvas, SKPoint grip)
    {
        var radius = HandleRadiusPixels * _worldPerPixel;
        var active = _isDragging || _isHovered;

        using var fill = canvas.GetSolidFillPaint(active ? AxisColor : new SKColor(0x20, 0x20, 0x20, 0xD0));
        using var ring = canvas.GetSimpleStrokePaint(1.5f * _worldPerPixel, SKColors.White);

        canvas.DrawCircle(grip, radius, fill);
        canvas.DrawCircle(grip, radius, ring);

        // A cross inside the disc reads as "move me".
        var arm = radius * 0.55f;
        canvas.DrawLine(grip.X - arm, grip.Y, grip.X + arm, grip.Y, ring);
        canvas.DrawLine(grip.X, grip.Y - arm, grip.X, grip.Y + arm, ring);
    }
}
