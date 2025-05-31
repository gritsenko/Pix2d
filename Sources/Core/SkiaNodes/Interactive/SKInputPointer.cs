using SkiaSharp;

namespace SkiaNodes.Interactive;

public readonly struct SKInputPointer
{
    public SKPoint ViewportPosition { get; }
    public SKPoint WorldPosition { get; }
    public bool IsPressed { get; }
    public bool IsTouch { get; }
    public bool IsEraser { get; }

    public SKInputPointer(SKPoint pos, ViewPort viewPort, bool isPointerPressed, bool isEraser, bool isTouch)
    {
            ViewportPosition = pos;
            WorldPosition = viewPort.ViewportToWorld(new SKPoint(pos.X, pos.Y));
            IsPressed = isPointerPressed;
            IsTouch = isTouch;
            IsEraser = isEraser;
        }

    public SKPoint GetPosition(SKNode relativeTo)
    {
            return relativeTo.GetLocalPosition(WorldPosition);
        }
}