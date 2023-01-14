using SkiaSharp;

namespace SkiaNodes.Interactive
{
    public interface IInteractive
    {
        void OnPointerPressed(PointerActionEventArgs eventArgs, int clickCount);
        void OnPointerReleasedCore(PointerActionEventArgs eventArgs);
        void OnPointerMoved(PointerActionEventArgs eventArgs);
        void OnPointerEnter(SKPoint pos);
        void OnPointerLeave(SKPoint pos);

        void OnPanModeChanged(bool isPanModeEnabled);

    }
}
