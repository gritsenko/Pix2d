using SkiaSharp;

namespace SkiaNodes.Interactive;

public readonly struct SKInputPointer
{
    public SKPoint ViewportPosition { get; }
    public SKPoint WorldPosition { get; }
    public bool IsPressed { get; }
    public bool IsTouch { get; }
    public bool IsEraser { get; }

    /// <summary>
    /// Normalized stylus pen pressure in the range [0..1]. Pointers that have no real pressure
    /// (mouse, plain touch) report <c>1</c>, so consumers that opt into pressure see "full pressure"
    /// for them and behave exactly as before.
    /// </summary>
    public float Pressure { get; }

    public SKInputPointer(SKPoint pos, ViewPort viewPort, bool isPointerPressed, bool isEraser, bool isTouch, float pressure = 1f)
    {
            ViewportPosition = pos;
            WorldPosition = viewPort.ViewportToWorld(new SKPoint(pos.X, pos.Y));
            IsPressed = isPointerPressed;
            IsTouch = isTouch;
            IsEraser = isEraser;
            Pressure = pressure;
        }

    public SKPoint GetPosition(SKNode relativeTo)
    {
            return relativeTo.GetLocalPosition(WorldPosition);
        }
}