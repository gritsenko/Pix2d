using SkiaSharp;

namespace SkiaNodes.Abstract
{
    public delegate void SetPointerReleasedHandler(SKPoint pos);
    public delegate void SetPointerPressedHandler(SKPoint pos);
    public interface IInputHandler
    {
        SetPointerReleasedHandler SetPointerReleased { get; set; }
        SetPointerPressedHandler SetPointerPressed { get; set; }
    }
}