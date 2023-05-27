using System;
using SkiaSharp;

namespace Pix2d.Shared;

public class SKImageView : ViewBase
{
    public static readonly DirectProperty<SKImageView, SKBitmapObservable> SourceProperty
        = AvaloniaProperty.RegisterDirect<SKImageView, SKBitmapObservable>(nameof(Source), o => o.Source, (o, v) => o.Source = v);

    private SKBitmapObservable _source;
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

    protected override object Build() =>
        new Border()
            .Ref(out _border)
            .Child(
                new Image()
                    //.StretchDirection(StretchDirection.DownOnly)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Ref(out _imageControl));

    private Image _imageControl;
    private SKBitmapObservable _bitmap;
    private Border _border;

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

    private void UpdateBitmapControl(SKBitmap newBitmap)
    {
        _imageControl.Source = newBitmap?.ToBitmap();

        if(newBitmap != null)
        {
            _imageControl.Width = newBitmap.Width;
            _imageControl.Height = newBitmap.Height;
        }
    }

    private void BitmapOnBitmapChanged(object sender, EventArgs e)
    {
        UpdateBitmapControl(_bitmap.Bitmap);
    }

    private void UpdateBackground(bool show)
    {
        if (show)
        {
            _border.Background = StaticResources.Brushes.CheckerTilesBrush;
        }
        else
        {
            _border.Background = Brushes.Transparent;
        }
    }

}