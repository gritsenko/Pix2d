using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Shared;

public class SKImageView : ViewBase
{
    public static readonly DirectProperty<SKImageView, SKBitmapObservable> SourceProperty
        = AvaloniaProperty.RegisterDirect<SKImageView, SKBitmapObservable>(nameof(Source), o => o.Source, (o, v) => o.Source = v);

    private SKBitmapObservable _source = null!;
    public SKBitmapObservable Source
    {
        get => _source;
        set
        {
            SetAndRaise(SourceProperty, ref _source, value);
            UpdateSourceBitmap(value);
        }
    }

    public static readonly DirectProperty<SKImageView, bool> ShowCheckerBackgroundProperty
        = AvaloniaProperty.RegisterDirect<SKImageView, bool>(nameof(ShowCheckerBackground), o => o.ShowCheckerBackground, (o, v) => o.ShowCheckerBackground = v);

    private bool _showCheckerBackground;
    public bool ShowCheckerBackground
    {
        get => _showCheckerBackground;
        set
        {
            SetAndRaise(ShowCheckerBackgroundProperty, ref _showCheckerBackground, value);
            UpdateBackground(value);
        }
    }

    public static readonly DirectProperty<SKImageView, bool> PixelPerfectProperty
        = AvaloniaProperty.RegisterDirect<SKImageView, bool>(nameof(PixelPerfect), o => o.PixelPerfect, (o, v) => o.PixelPerfect = v);

    private bool _pixelPerfect;

    /// <summary>When true, the bitmap scales with nearest-neighbour (crisp pixels) instead of the framework
    /// default smoothing — use for pixel-art that may be up-scaled to fit the view. Off leaves thumbnails smooth.</summary>
    public bool PixelPerfect
    {
        get => _pixelPerfect;
        set
        {
            SetAndRaise(PixelPerfectProperty, ref _pixelPerfect, value);
            ApplyInterpolationMode();
        }
    }

    protected override object Build()
    {
        _imageControl = new Image()
            .HorizontalAlignment(HorizontalAlignment.Center)
            .VerticalAlignment(VerticalAlignment.Center);

        ApplyInterpolationMode();

        _border = new Border().Child(_imageControl);
        return _border;
    }

    private void ApplyInterpolationMode()
    {
        if (_imageControl == null)
            return;

        // Only opt in to nearest-neighbour; otherwise leave the framework default so downscaled thumbnails stay smooth.
        if (_pixelPerfect)
            RenderOptions.SetBitmapInterpolationMode(_imageControl, BitmapInterpolationMode.None);
    }

    private Image _imageControl = null!;
    private SKBitmapObservable _bitmap = null!;
    private Border _border = null!;

    private void UpdateSourceBitmap(SKBitmapObservable newBitmap)
    {
        if (_bitmap != null)
        {
            _bitmap.BitmapChanged -= BitmapOnBitmapChanged;
        }

        _bitmap = newBitmap;

        if (_bitmap != null)
        {
            _bitmap.BitmapChanged += BitmapOnBitmapChanged;
        }

        UpdateBitmapControl(_bitmap?.Bitmap);
    }

    private void UpdateBitmapControl(SKBitmap? newBitmap)
    {
        if(newBitmap != null && (newBitmap.Width < 1 || newBitmap.Height < 1))
        {
            newBitmap = null;
        }

        if (_imageControl == null)
            return;

        _imageControl.Source = newBitmap?.ToBitmap();
    }

    private void BitmapOnBitmapChanged(object? sender, EventArgs e)
    {
        UpdateBitmapControl(_bitmap.Bitmap);
    }

    private void UpdateBackground(bool show)
    {
        if (_border == null)
            return;

        if (show)
        {
            _border.Background = StaticResources.Brushes.CheckerBrush;
        }
        else
        {
            _border.Background = Brushes.Transparent;
        }
    }

}