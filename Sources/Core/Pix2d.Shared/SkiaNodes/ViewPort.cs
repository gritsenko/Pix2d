using System;
using System.Collections.Generic;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace SkiaNodes
{
    public class ViewPortSettings
    {
        public bool RenderAdorners { get; set; } = true;
    }

    public class ViewPort
    {

        private List<float> _zoomGrid = new List<float>()
        {
            3.13f,4.17f, 6.25f, 8.33f, 12f, 16.67f, 25f, 33.33f, 50, 66.67f,
            100,
            150,200,300,400,600,800,1200,1600,2400,3200,4800,6400,8500,12750,17000,25500,34000,51000,64000
        };

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

        public event EventHandler ViewChanged;
        public event EventHandler RefreshRequested;

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
            for (var i = 1; i < _zoomGrid.Count; i++)
            {
                if (zoom < (float)(Math.Round(_zoomGrid[i] / 100f, 2)))
                    return i - 1;
            }

            return _zoomGrid.Count - 1;
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
            var i = GetZoomGridIndex(Zoom);

            if (i > -1)
            {
                i = CoerceZoomIndex(i + Math.Sign(direction));
                var newZoom = _zoomGrid[i] / 100f;
                SetZoom(newZoom, centerPointOnViewport);
            }
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

            var oldPos = ViewportToWorld(centerPointOnViewport);

            if (newZoom <= MinZoom)
            {
                newZoom = MinZoom;
            }
            if (newZoom > MaxZoom)
            {
                newZoom = MaxZoom;
            }

            if (Math.Abs(newZoom - Zoom) > 0.01)
            {
                Zoom = (float)Math.Round(newZoom, 2);
            }
            else
            {
                Zoom = (float)Math.Round(newZoom, 4);
            }

            //OnZoomChanged();
            CalculateTransform();

            var deltaPan = (ViewportToWorld(centerPointOnViewport) - oldPos).Multiply(new SKPoint(TransformMatrix.ScaleX,
                TransformMatrix.ScaleY));

            if (Math.Abs(deltaPan.X) > 0.01 || Math.Abs(deltaPan.Y) > 0.01)
                ChangePan(-deltaPan.X, -deltaPan.Y);
        }

        private float Snap(float x, float gridStep)
        {
            return (float)Math.Round(x * gridStep) / gridStep;
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
            rawX = (float)Math.Floor(rawX);
            rawY = (float)Math.Floor(rawY);
            Pan = new SKPoint(rawX, rawY);
            OnPanChanged();
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
            var trans = SKMatrix.CreateTranslation(-Pan.X, -Pan.Y);
            var scale = SKMatrix.CreateScale(DpiEffectiveZoom, DpiEffectiveZoom);
            SKMatrix.Concat(ref TransformMatrix, trans, scale);
        }

        protected virtual void OnPanChanged()
        {
            OnViewChanged();
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
}