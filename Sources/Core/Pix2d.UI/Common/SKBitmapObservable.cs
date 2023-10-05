using System;
using Mvvm;
using Pix2d.Abstract.UI;
using SkiaSharp;

namespace Pix2d.Common;

public class SKBitmapObservable : ObservableObject, ISKBitmapObservable
{
    public event EventHandler BitmapChanged;
    public SKBitmap Bitmap { get; set; }

    public void SetBitmap(SKBitmap bitmap)
    {
        Bitmap = bitmap;
        OnPropertyChanged(nameof(Bitmap));
        Bitmap.NotifyPixelsChanged();
        OnBitmapChanged();
    }

    public void RaiseBitmapChanged()
    {
        OnBitmapChanged();
    }

    protected virtual void OnBitmapChanged()
    {
        RunInUiThread(() =>
        {
            BitmapChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}