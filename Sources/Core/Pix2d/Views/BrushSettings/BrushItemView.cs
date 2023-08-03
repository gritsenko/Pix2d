using System;
using Mvvm;
using SkiaSharp;

namespace Pix2d.Views.BrushSettings;

public class BrushItemView : ComponentBase
{
    private const int DefaultSize = 32;
    private Primitives.Drawing.BrushSettings _preset;

    protected override object Build() =>
        new Grid()
            .Background(() => PreviewBitmap.ToBrush());


    public Primitives.Drawing.BrushSettings Preset
    {
        get => _preset;
        set
        {
            _preset = value;
            UpdatePreview();
            UpdateViewSize();
            
            StateHasChanged();
        }
    }

    public SKBitmapObservable Preview { get; set; } = new();

    public SKBitmap PreviewBitmap => Preview.Bitmap;

    public double Scale
    {
        get => Preset?.Scale ?? 1;
        set
        {
            Preset.Scale = value > 0 ? (float)value : 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeStr));
            UpdatePreview();
            UpdateBrush();
        }
    }

    public double Opacity
    {
        get => Preset?.Opacity ?? 1;
        set
        {
            Preset.Opacity = (float)value;
            
            UpdatePreview();
            UpdateBrush();
            
            OnPropertyChanged();
        }
    }

    private void UpdateViewSize()
    {
        if (!double.IsFinite(Width))
        {
            Width = DefaultSize;
        }

        if (!double.IsFinite(Height))
        {
            Height = DefaultSize;
        }
    }

    [NotifiesOn(nameof(Scale))]
    public string SizeStr => Scale + "px";

    protected override void OnAfterInitialized()
    {

    }

    private void UpdatePreview()
    {
        if (Preset == null) return;

        var size = double.IsFinite(Width + Height) ? (int)Math.Min(Width, Height) : DefaultSize;
        var src = Preset.Brush.GetPreview((float)Scale);
        if (src == null)
            return;

        var x = (int)(0.5f * (size - src.Width));
        var y = (int)(0.5f * (size - src.Height));

        if (Preview.Bitmap == null)
        {
            Preview.SetBitmap(new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        using var canvas = new SKCanvas(Preview.Bitmap);
        canvas.Clear();
        canvas.DrawBitmap(src, x, y);

        Preview.RaiseBitmapChanged();
    }

    public void UpdateBrush()
    {
        Preset?.InitBrush();
    }
}