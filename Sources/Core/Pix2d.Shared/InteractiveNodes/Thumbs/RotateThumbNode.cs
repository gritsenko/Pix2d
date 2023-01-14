using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d.CommonNodes.Controls.Thumbs
{
    public class RotateThumbNode : NodeManipulateThumbBase, IViewPortBindable
    {
        public SKColor StrokeColor = SKColor.Parse("#ff4384de");
        private SKPoint _initialThumbPos;
        private Dictionary<SKNode, SKPoint> _initialTargetsPos;
        private ViewPort _bindedViewPort;
        private SKPoint _borderMidPoint;
        private float _initialRotation;

        public Func<bool> AngleLockProviderFunc { get; set; }

        public RotateThumbNode()
        {
            Size = new SKSize(24, 24);
            PivotPosition = new SKPoint(12, 12);

            DragStarted += OnDragStarted;
            DragDelta += OnDragDelta;
            DragComplete += OnDragComplete;

        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            _initialThumbPos = GetGlobalPosition();

            _initialTargetsPos = TargetSelection.Nodes.ToDictionary(x => x, x => x.GetGlobalPosition());

            _initialRotation = TargetSelection.Rotation;
        }

        private void OnDragComplete(object sender, DragCompletedEventArgs e)
        {
            _initialTargetsPos = null;
        }

        private void OnDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_initialTargetsPos == null)
                return;

            var delta = new SKPoint(e.HorizontalChange, e.VerticalChange);

            var newPos = _initialThumbPos + delta;

            var frame = TargetSelection.Frame;
            var centerPoint = frame.GetGlobalTransform().MapPoint(new SKPoint(frame.Size.Width / 2f, frame.Size.Height / 2f));

            var angle = (float) ( Math.Atan2(newPos.X - centerPoint.X, newPos.Y - centerPoint.Y) * (180 / Math.PI) );

            if (AngleLockProviderFunc?.Invoke() == true)
            {
                angle = angle.SnapToGrid(5);
            }
            else
            {
                angle = angle.SnapToGrid(1);
            }

            TargetSelection.Rotation = 180f - angle;
        }

        protected override void AdjustDimensionsToTargets(NodesSelection selection)
        {
            if(selection == null || _bindedViewPort == null)
                return;

            var frame = selection.Frame;
            var x0 = frame.Size.Width / 2f;
            var midPoint = new SKPoint(x0, -_bindedViewPort.PixelsToWorld(64));
            _borderMidPoint = frame.GetGlobalTransform().MapPoint(new SKPoint(x0, 0));
            Position = frame.GetGlobalTransform().MapPoint(midPoint);
        }

        public override void OnDraw(SKCanvas canvas, ViewPort vp)
        {

            var hz = GetHitZone();

            var w = Size.Width;
            var h = Size.Height;
            canvas.Save();
            var transform = vp.ResultTransformMatrix;
            canvas.SetMatrix(transform);

            using (var strokePaint = new SKPaint())
            using (var fillPaint = new SKPaint())
            {
                strokePaint.IsStroke = true;
                strokePaint.IsAntialias = true;
                strokePaint.StrokeWidth = vp.PixelsToWorld(2);
                strokePaint.Color = StrokeColor;

                fillPaint.Color = SKColors.White;


                canvas.DrawLine(hz.MidX, hz.MidY, _borderMidPoint.X, _borderMidPoint.Y, strokePaint);
                canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(12), fillPaint);
                canvas.DrawCircle(hz.MidX, hz.MidY, vp.PixelsToWorld(12), strokePaint);
            }
            canvas.Restore();

            //DrawBoundingBox(canvas, vp, 2, StrokeColor);
        }

        public void OnViewChanged(ViewPort vp)
        {
            _bindedViewPort = vp;

            var size = vp.PixelsToWorld(24);
            Size = new SKSize(size, size);
            PivotPosition = new SKPoint(size/2, size/2);

            AdjustDimensionsToTargets(TargetSelection);
        }
    }
}