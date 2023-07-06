using Mvvm;
using SkiaSharp;
using System.Threading.Tasks;

namespace Pix2d.Views.BrushSettings;

public class BrushItemView : ComponentBase
{
    private Primitives.Drawing.BrushSettings _preset;

    protected override object Build() =>
        new Grid()
            .Background(() => Preview.Bitmap.ToBrush())
            .Width(48)
            .Height(48);


    public Primitives.Drawing.BrushSettings Preset
    {
        get => _preset;
        set
        {
            _preset = value;
            StateHasChanged();
        }
    }

    public SKBitmapObservable Preview { get; set; } = new();

    public double Scale
    {
        get => Preset?.Scale ?? 1;
        set
        {
            Preset.Scale = value > 0 ? (float)value : 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SizeStr));
            UpdatePreviewAsync();
            UpdateBrush();
        }
    }

    public double Opacity
    {
        get => Preset?.Opacity ?? 1;
        set
        {
            Preset.Opacity = (float)value;
            OnPropertyChanged();
            UpdatePreviewAsync();
            UpdateBrush();
        }
    }

    [NotifiesOn(nameof(Scale))]
    public string SizeStr => Scale + "px";

    //public BrushPresetViewModel(BrushSettings preset)
    //{
    //    Preset = preset;
    //    UpdatePreviewAsync();
    //}
    protected override void OnAfterInitialized()
    {
    }

    private async void UpdatePreviewAsync()
    {
        const int size = 36;
        var src = Preset.Brush.GetPreview((float)Scale);
        if (src == null)
            return;

        var x = (int)(0.5f * (size - src.Width));
        var y = (int)(0.5f * (size - src.Height));

        if (Preview.Bitmap == null)
        {
            Preview.SetBitmap(new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        await Task.Run(() =>
        {
            using var canvas = new SKCanvas(Preview.Bitmap);
            canvas.Clear();
            canvas.DrawBitmap(src, x, y);
        });

        Preview.RaiseBitmapChanged();
    }

    public void UpdateBrush()
    {
        Preset?.InitBrush();
    }
}