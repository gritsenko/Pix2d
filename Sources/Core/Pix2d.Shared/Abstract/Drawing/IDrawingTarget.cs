using SkiaSharp;

namespace Pix2d.Abstract.Drawing;

public interface IDrawingTarget
{
    /// <summary>
    /// Executed when layer or frame is changed, to apply current selection before change target bitmap
    /// </summary>
    Action FlushRequestedAction { set; }

    bool LockTransparentPixels { get; }

    //void InvalidateBitmap();
    byte[] GetData();

    void HideTargetBitmap();
    void ShowTargetBitmap();
        
    // If set the render target will use the image returned by the substitute function instead of actual image.
    void SetTargetBitmapSubstitute(Func<SKBitmap> substitute);

    bool IsTargetBitmapVisible();

    float GetOpacity();
    SKSize GetSize();
    SKColor PickColorByPoint(int localPosX, int localPosY);
    void CopyBitmapTo(SKBitmap workingBitmap);

    //Changing bitmap methods
    void Draw(Action<SKCanvas> drawAction);
    void ModifyBitmap(Action<SKBitmap> processAction);
    void EraseBitmap();
    void SetData(byte[] data);
}