using SkiaNodes.Extensions;
using SkiaSharp;

namespace SkiaNodes;

public class ViewPortSettings
{
    public bool RenderAdorners { get; set; } = true;
}

public class ViewPort
{
    private const int ZoomPrecisionDigits = 4;
    private const float ZoomGridTolerance = 0.00005f;
    private const float ViewStateTolerance = 0.01f;

    private readonly List<float> _zoomGrid =
    [
        3.13f, 4.17f, 6.25f, 8.33f, 12f, 16.67f, 25f, 33.33f, 50, 66.67f,
        100,
        150, 200, 300, 400, 600, 800, 1200, 1600, 2400, 3200, 4800, 6400, 8500, 12750, 17000, 25500, 34000, 51000, 64000
    ];

    private SKMatrix TransformMatrix = SKMatrix.CreateIdentity();
    private float _scaleFactor = 1;

    public SKMatrix PivotTransformMatrix = SKMatrix.CreateIdentity();
    public SKMatrix ResultTransformMatrix => TransformMatrix.PostConcat(PivotTransformMatrix);
    public ViewPortSettings Settings { get; set; } = new ViewPortSettings();

    public float ScaleFactor
    {
        get => _scaleFactor;
        set
        {
            _scaleFactor = value;
            CalculateTransform();
        }
    }

    public const float MaxZoom = 100;
    public const float MinZoom = 0.01f;

    public event EventHandler? ViewChanged;
    public event EventHandler? RefreshRequested;

    public bool IsPixelPerfectZoom => Math.Abs(DpiEffectiveZoom - 1) < 0.0000001f || Math.Abs((DpiEffectiveZoom % 2)) < 0.00000001f;

    /// <summary>
    /// Position of viewport center point relative to world coordinates
    /// </summary>
    public SKPoint ViewPortCenterGlobal => ViewportToWorld(new SKPoint(Size.Width * ScaleFactor / 2, Size.Height * ScaleFactor / 2));

    /// <summary>
    /// Local position of viewport center point. Half of viewport size
    /// </summary>
    public SKPoint ViewPortCenter => new SKPoint(Size.Width / 2, Size.Height / 2);

    public SKPoint Pan { get; private set; }

    public float Zoom { get; set; } = 1;

    public float DpiEffectiveZoom => ScaleFactor * Zoom;

    public SKSize Size { get; set; }

    public Func<SKRect?>? ContentBoundsProvider { get; set; }

    public float MinVisibleContentPixels { get; set; }

    public ViewPort(int width, int height)
    {
        Size = new SKSize(width, height);
    }

    public void ChangeZoom(float dw, SKPoint centerPointOnViewport = default(SKPoint), bool round = false)
    {
        SetZoom(Zoom * dw, centerPointOnViewport, round);
    }

    public int GetZoomGridIndex(float zoom)
    {
        var normalizedZoom = NormalizeZoom(zoom);

        for (var i = _zoomGrid.Count - 1; i >= 0; i--)
        {
            if (normalizedZoom + ZoomGridTolerance >= GetZoomGridValue(i))
                return i;
        }

        return 0;
    }

    public int CoerceZoomIndex(int zoomIndex)
    {
        return Math.Max(0, Math.Min(_zoomGrid.Count - 1, zoomIndex));
    }

    public void ZoomIn(SKPoint centerPointOnViewport = default(SKPoint))
    {
        ZoomByGrid(1, centerPointOnViewport);
    }
    public void ZoomOut(SKPoint centerPointOnViewport = default(SKPoint))
    {
        ZoomByGrid(-1, centerPointOnViewport);
    }

    public void ZoomByGrid(float direction, SKPoint centerPointOnViewport = default(SKPoint))
    {
        var step = Math.Sign(direction);
        if (step == 0)
            return;

        var normalizedZoom = NormalizeZoom(Zoom);

        if (step > 0)
        {
            for (var i = 0; i < _zoomGrid.Count; i++)
            {
                var gridZoom = GetZoomGridValue(i);
                if (gridZoom > normalizedZoom + ZoomGridTolerance)
                {
                    SetZoom(gridZoom, centerPointOnViewport);
                    return;
                }
            }

            SetZoom(GetZoomGridValue(_zoomGrid.Count - 1), centerPointOnViewport);
            return;
        }

        for (var i = _zoomGrid.Count - 1; i >= 0; i--)
        {
            var gridZoom = GetZoomGridValue(i);
            if (gridZoom < normalizedZoom - ZoomGridTolerance)
            {
                SetZoom(gridZoom, centerPointOnViewport);
                return;
            }
        }

        SetZoom(Math.Min(normalizedZoom, GetZoomGridValue(0)), centerPointOnViewport);
    }

    public void ZoomAddPercent(int percent, SKPoint centerPointOnViewport = default(SKPoint))
    {
        var zoomDelta = 0.01f * percent;
        var newZoom = Zoom + zoomDelta;

        SetZoom(newZoom, centerPointOnViewport);
    }

    public void SetZoom(float newZoom, SKPoint centerPointOnViewport = default(SKPoint), bool round = false)
    {
        if (centerPointOnViewport == default(SKPoint))
        {
            centerPointOnViewport = ViewPortCenter.Multiply(ScaleFactor);
        }

        var oldZoom = Zoom;
        var oldPos = ViewportToWorld(centerPointOnViewport);

        if (newZoom <= MinZoom)
        {
            newZoom = MinZoom;
        }
        if (newZoom > MaxZoom)
        {
            newZoom = MaxZoom;
        }

        //if (Math.Abs(newZoom - Zoom) > 0.01)
        //{
        //    Zoom = (float)Math.Round(newZoom, 2);
        //}
        //else
        //{
        //    Zoom = (float)Math.Round(newZoom, 4);
        //}
        newZoom = NormalizeZoom(newZoom);

        if (Math.Abs(newZoom - Zoom) < ZoomGridTolerance)
        {
            ClampPanToVisibleContent();
            return;
        }

        Zoom = newZoom;
        CalculateTransform();

        var deltaPan = (ViewportToWorld(centerPointOnViewport) - oldPos).Multiply(new SKPoint(TransformMatrix.ScaleX,
            TransformMatrix.ScaleY));

        var newPan = Pan;
        if (Math.Abs(deltaPan.X) > ViewStateTolerance || Math.Abs(deltaPan.Y) > ViewStateTolerance)
        {
            newPan = new SKPoint(Pan.X - deltaPan.X, Pan.Y - deltaPan.Y);
        }

        var clampedPan = CoercePan(newPan);
        var panChanged = !AreClose(Pan, clampedPan);

        Pan = clampedPan;

        if (Math.Abs(Zoom - oldZoom) > ZoomGridTolerance || panChanged)
        {
            OnViewChanged();
        }
    }

    private float Snap(float x, float gridStep)
    {
        return (float)Math.Round(x * gridStep) / gridStep;
    }

    private static float NormalizeZoom(float zoom)
    {
        return (float)Math.Round(zoom, ZoomPrecisionDigits);
    }

    private float GetZoomGridValue(int index)
    {
        return NormalizeZoom(_zoomGrid[index] / 100f);
    }

    public static double Floor(double value, int decimalPlaces)
    {
        double adjustment = Math.Pow(10, decimalPlaces);
        return Math.Floor(value * adjustment) / adjustment;
    }


    public void ChangePan(float rawDx, float rawDy)
    {
        SetPan(Pan.X + rawDx, Pan.Y + rawDy);
    }

    public void SetPan(float rawX, float rawY)
    {
        var newPan = CoercePan(new SKPoint(rawX, rawY));
        if (AreClose(Pan, newPan))
            return;

        Pan = newPan;
        OnPanChanged();
    }

    public void UpdateViewportMetrics(SKSize newSize, float newScaleFactor, bool preserveFraming = true)
    {
        var sizeChanged = !AreClose(Size, newSize);
        var scaleChanged = Math.Abs(_scaleFactor - newScaleFactor) > ZoomGridTolerance;

        if (!sizeChanged && !scaleChanged)
        {
            ClampPanToVisibleContent();
            return;
        }

        var hasOldMetrics = HasValidViewportMetrics(Size, _scaleFactor);
        var hasNewMetrics = HasValidViewportMetrics(newSize, newScaleFactor);
        var preserveViewState = preserveFraming && hasOldMetrics && hasNewMetrics;

        var centerWorld = preserveViewState ? ViewPortCenterGlobal : default;
        var newZoom = Zoom;

        if (preserveViewState)
        {
            var widthRatio = (newSize.Width * newScaleFactor) / (Size.Width * _scaleFactor);
            var heightRatio = (newSize.Height * newScaleFactor) / (Size.Height * _scaleFactor);
            var zoomRatio = Math.Min(widthRatio, heightRatio);

            if (!float.IsNaN(zoomRatio) && !float.IsInfinity(zoomRatio) && zoomRatio > 0)
            {
                newZoom = NormalizeZoom(Math.Max(MinZoom, Math.Min(MaxZoom, Zoom * zoomRatio)));
            }
        }

        Size = newSize;
        _scaleFactor = newScaleFactor;
        Zoom = newZoom;

        if (preserveViewState)
        {
            Pan = GetPanForCenter(centerWorld);
        }

        Pan = CoercePan(Pan);
        OnViewChanged();
    }

    public void ClampPanToVisibleContent()
    {
        var clampedPan = CoercePan(Pan);
        if (AreClose(Pan, clampedPan))
            return;

        Pan = clampedPan;
        OnViewChanged();
    }

    public void ScrollTo(SKRect bounds, float topLeftMargin)
    {
        SetZoom(1);
        var margin = topLeftMargin * ScaleFactor;

        CenterView(bounds);

        if (Size.Width < bounds.Width * ScaleFactor + margin)
        {
            SetPan(bounds.Left * ScaleFactor - margin, Pan.Y);
        }

        if (Size.Height < bounds.Height * ScaleFactor + margin)
        {
            SetPan(Pan.X, bounds.Top * ScaleFactor - margin);
        }
    }

    public void ShowArea(SKRect bounds, SKSize margin = default, float maxZoom = -1f)
    {
        var zoom = 1d;
        var vertZoom = (Size.Height - margin.Height) / (bounds.Height);
        var horZoom = (Size.Width - margin.Width) / (bounds.Width);

        zoom = Math.Min(vertZoom, horZoom);// / ScaleFactor;

        //if (maxZoom > -1)
        //    zoom = Math.Min(zoom, maxZoom) / ScaleFactor;

        SetZoom((float)zoom);

        CenterView(bounds);
    }

    public void ShowArea(SKRect bounds, float leftMargin, float topMargin, float rightMargin, float bottomMargin, float maxZoom = -1f)
    {
        var relativeleftMargin = leftMargin;
        var relativeTopMargin = topMargin;
        var relativeRightMargin = rightMargin;
        var relatiiveBottomMargin = bottomMargin;

        var zoom = 1d;
        var verticalZoom = (Size.Height) / (bounds.Height + relativeTopMargin + relatiiveBottomMargin);
        var horisontalZoom = (Size.Width) / (bounds.Width + relativeleftMargin + relativeRightMargin);

        zoom = Math.Min(verticalZoom, horisontalZoom);

        if (maxZoom > -1)
            zoom = Math.Min(zoom, maxZoom);

        zoom = zoom / ScaleFactor;

        SetZoom((float)zoom);

        var p0 = bounds.GetLeftTopPoint() - new SKPoint(leftMargin, topMargin);
        var p1 = bounds.GetRightBottomPoint() + new SKPoint(rightMargin, bottomMargin);
        var newRect = new SKRect(p0.X, p0.Y, p1.X, p1.Y);
        CenterView(newRect);
    }


    public SKPoint ViewportToWorld(SKPoint positionInViewport)
    {
        TransformMatrix.TryInvert(out var inverted);
        return inverted.MapPoint(positionInViewport);
        //return positionInViewport.ApplyInvertedTransform(TransformMatrix);
    }

    public SKPoint WorldToViewport(SKPoint globalPosition)
    {
        return TransformMatrix.MapPoint(globalPosition);
    }

    public SKRect WorldToViewport(SKRect globalRect)
    {
        var p0 = WorldToViewport(globalRect.GetLeftTopPoint());
        var p1 = WorldToViewport(globalRect.GetRightBottomPoint());
        return SKPointExtensions.ToSKRect(p0, p1);
    }

    public SKRect ViewportToWorld(SKRect rectOnViewport)
    {
        var p0 = ViewportToWorld(rectOnViewport.GetLeftTopPoint());
        var p1 = ViewportToWorld(rectOnViewport.GetRightBottomPoint());
        return SKPointExtensions.ToSKRect(p0, p1);
    }


    public SKRect GetVisibleArea()
    {
        return ViewportToWorld(new SKRect(0, 0, Size.Width * ScaleFactor, Size.Height * ScaleFactor));
    }

    public float PixelsToWorld(float pixelsLength)
    {
        return pixelsLength / DpiEffectiveZoom;
    }

    public void CenterView(SKRect bounds = default(SKRect))
    {
        if (bounds == default(SKRect))
            SetPan(-Size.Width / 2, -Size.Height / 2);
        else
        {
            var c = new SKPoint(
                bounds.MidX * DpiEffectiveZoom,
                bounds.MidY * DpiEffectiveZoom) - ViewPortCenter.Multiply(ScaleFactor);
            //                c = new SKPoint(- ViewPortCenter.X*ScaleFactor, -ViewPortCenter.Y*ScaleFactor);
            SetPan(c.X, c.Y);
        }
    }

    protected virtual void OnZoomChanged()
    {
        OnViewChanged();
    }

    private void OnViewChanged()
    {
        CalculateTransform();
        ViewChanged?.Invoke(this, EventArgs.Empty);
        Refresh();
    }

    private void CalculateTransform()
    {
        var trans = SKMatrix.CreateTranslation(-(float)Math.Round(Pan.X), -(float)Math.Round(Pan.Y));
        var scale = SKMatrix.CreateScale(DpiEffectiveZoom, DpiEffectiveZoom);
        SKMatrix.Concat(ref TransformMatrix, trans, scale);
    }

    protected virtual void OnPanChanged()
    {
        OnViewChanged();
    }

    private SKPoint CoercePan(SKPoint rawPan)
    {
        if (!TryGetPanClampBounds(out var minPanX, out var maxPanX, out var minPanY, out var maxPanY))
            return rawPan;

        var x = Math.Max(minPanX, Math.Min(maxPanX, rawPan.X));
        var y = Math.Max(minPanY, Math.Min(maxPanY, rawPan.Y));

        return new SKPoint(x, y);
    }

    private bool TryGetPanClampBounds(out float minPanX, out float maxPanX, out float minPanY, out float maxPanY)
    {
        minPanX = maxPanX = minPanY = maxPanY = 0;

        if (ContentBoundsProvider == null || MinVisibleContentPixels <= 0)
            return false;

        var bounds = ContentBoundsProvider();
        if (!bounds.HasValue)
            return false;

        var contentBounds = bounds.Value;
        if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
            return false;

        var viewportWidth = Size.Width * ScaleFactor;
        var viewportHeight = Size.Height * ScaleFactor;
        if (viewportWidth <= 0 || viewportHeight <= 0 || DpiEffectiveZoom <= 0)
            return false;

        var scaledLeft = contentBounds.Left * DpiEffectiveZoom;
        var scaledRight = contentBounds.Right * DpiEffectiveZoom;
        var scaledTop = contentBounds.Top * DpiEffectiveZoom;
        var scaledBottom = contentBounds.Bottom * DpiEffectiveZoom;

        var visibleWidth = Math.Min(MinVisibleContentPixels, Math.Min(viewportWidth, scaledRight - scaledLeft));
        var visibleHeight = Math.Min(MinVisibleContentPixels, Math.Min(viewportHeight, scaledBottom - scaledTop));

        if (visibleWidth <= 0 || visibleHeight <= 0)
            return false;

        minPanX = scaledLeft - (viewportWidth - visibleWidth);
        maxPanX = scaledRight - visibleWidth;
        minPanY = scaledTop - (viewportHeight - visibleHeight);
        maxPanY = scaledBottom - visibleHeight;

        return minPanX <= maxPanX && minPanY <= maxPanY;
    }

    private SKPoint GetPanForCenter(SKPoint centerWorld)
    {
        var viewportWidth = Size.Width * ScaleFactor;
        var viewportHeight = Size.Height * ScaleFactor;

        return new SKPoint(
            centerWorld.X * DpiEffectiveZoom - viewportWidth / 2,
            centerWorld.Y * DpiEffectiveZoom - viewportHeight / 2);
    }

    private static bool HasValidViewportMetrics(SKSize size, float scaleFactor)
    {
        return size.Width > 0 && size.Height > 0 && scaleFactor > 0;
    }

    private static bool AreClose(SKPoint left, SKPoint right)
    {
        return Math.Abs(left.X - right.X) < ViewStateTolerance && Math.Abs(left.Y - right.Y) < ViewStateTolerance;
    }

    private static bool AreClose(SKSize left, SKSize right)
    {
        return Math.Abs(left.Width - right.Width) < ViewStateTolerance && Math.Abs(left.Height - right.Height) < ViewStateTolerance;
    }

    public void SnapToPercentGrid(int perecentStep, SKPoint viewPortViewPortCenter)
    {
        var newZoom = Snap(Zoom, 0.01f * perecentStep);
        SetZoom(newZoom, viewPortViewPortCenter);
    }

    public void Refresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}