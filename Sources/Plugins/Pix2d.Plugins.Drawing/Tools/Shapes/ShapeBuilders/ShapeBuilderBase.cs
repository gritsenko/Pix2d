using Pix2d.Abstract.Drawing;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.Shapes;

public abstract class ShapeBuilderBase
{
    protected IDrawingLayer DrawingLayer { get; set; }
    protected List<SKPoint> Points = new List<SKPoint>();

    public AddPointInputMode AddPointInputMode = AddPointInputMode.PressAndHold;

    public virtual void Initialize(IDrawingLayer drawingLayer)
    {
        DrawingLayer = drawingLayer;
    }

    public virtual void AddPoint(SKPoint point)
    {
        Points.Add(point);
        OnPointAdded(point, Points.Count);
    }

    protected abstract void OnPointAdded(SKPoint point, int pointsCount);

    public abstract void SetNextPointPreview(SKPoint previewPoint);


    public void BeginDrawing()
    {
        DrawingLayer.BeginDrawing();
    }

    public void Finish()
    {
        DrawingLayer.FinishDrawing();
        Points.Clear();
    }

    public void Cancel()
    {
        DrawingLayer.FinishDrawing(cancel: true);
        Points.Clear();
    }

    protected SKPoint GetMirroredPoint(SKPoint p)
    {
        return DrawingLayer.GetMirroredPoint(p.ToSkPointI());
    }

    public void Reset()
    {
        Cancel();
    }
}