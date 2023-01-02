using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using Avalonia.Controls.Shapes;

namespace Pix2d.Shared;

public class ColorPicker : ViewBase
{
    public static readonly DirectProperty<ColorPicker, SKColor> ColorProperty
        = AvaloniaProperty.RegisterDirect<ColorPicker, SKColor>(nameof(Color), o => o.Color, (o, v) => o.Color = v);
    private SKColor _color = default;
    public SKColor Color
    {
        get => _color;
        set
        {
            SetAndRaise(ColorProperty, ref _color, value);

            if (!_isPrivateUpdate)
            {
                UpdateColorOutside(value);
                UpdateHueColor();
                UpdateThumb();
            }
        }
    }

    public static readonly DirectProperty<ColorPicker, Color> ColorHueProperty
        = AvaloniaProperty.RegisterDirect<ColorPicker, Color>(nameof(ColorHue), o => o.ColorHue, (o, v) => o.ColorHue = v);
    private Color _colorHue = default;
    public Color ColorHue
    {
        get => _colorHue;
        set => SetAndRaise(ColorHueProperty, ref _colorHue, value);
    }


    private IAssetLoader AssetLoader => AvaloniaLocator.Current.GetService<IAssetLoader>();
    private IBitmap ThumbImage => new Bitmap(AssetLoader.Open(new Uri("avares://Pix2d.Editor/Assets/ColorThumb.png")));

    protected override object Build() =>
        new Grid()
            .Cols("*, 34")
            .Children(
                GetColorSquare()
                    .Ref(out _colorSquare),

                HueSlider
                    .Ref(out _hueSlider)
                    .Margin(6, 0)
                    .Col(1)
            );

    private Control GetColorSquare() => new Border()
    .CornerRadius(5)
    .ClipToBounds(true)
    .Child(
        new Grid()
            .Children(
                new Rectangle()
                    .Fill(new LinearGradientBrush()
                    {
                        StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1.0, 0.5, RelativeUnit.Relative),
                        GradientStops =
                        {
                                new GradientStop {Color = Colors.White, Offset = 0},
                                new GradientStop {[!GradientStop.ColorProperty] = Bind(this, vm=>vm.ColorHue), Offset = 1}
                        }
                    }),

                new Rectangle()
                    .Fill(new LinearGradientBrush()
                    {
                        StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                        GradientStops =
                        {
                                new GradientStop {Color = new Color(), Offset = 0},
                                new GradientStop {Color = Colors.Black, Offset = 1}
                        }
                    }),
                new Image() { IsHitTestVisible = false }
                    .Ref(out _colorThumb)
                    .Width(32)
                    .Height(32)
                    .Margin(-16)
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .VerticalAlignment(VerticalAlignment.Top)
                    .Source(ThumbImage)
            )
    );

    private Control HueSlider => new Grid()
        .Children(
            new Border()
                .CornerRadius(5)
                .Background(new LinearGradientBrush()
                {
                    StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop {Color= "#FFFF0000".ToColor() },
                        new GradientStop {Color= "#FEFFFF00".ToColor(), Offset=0.167 },
                        new GradientStop {Color= "#FE00FF00".ToColor(), Offset=0.333 },
                        new GradientStop {Color= "#FE00FFFF".ToColor(), Offset=0.5 },
                        new GradientStop {Color= "#FE0000FF".ToColor(), Offset=0.667 },
                        new GradientStop {Color= "#FEFF00FF".ToColor(), Offset=0.833 },
                        new GradientStop {Color= "#FFFF0000".ToColor(), Offset=1.0 },
                    }
                })
                .Child(
                    new Image() { IsHitTestVisible = false }
                        .Ref(out _hueThumb)
                        .Width(32)
                        .Height(32)
                        .Margin(-5, -16, 0, 0)
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .VerticalAlignment(VerticalAlignment.Top)
                        .Source(ThumbImage)
                )
        );



    private Control _colorSquare;
    private Control _hueSlider;
    private Image _colorThumb;
    private Image _hueThumb;

    private Size SquareSize => _colorSquare.Bounds.Size;
        
    private double _sat;
    private double _val;
    private double _hue;
    private bool _isPrivateUpdate = false;
    private SKColor _colHue;
    private double _tickHeight => _hueSlider.Bounds.Height / 360;

    protected override void OnAfterInitialized()
    {
        _colorThumb.RenderTransform = new TranslateTransform();
        _hueThumb.RenderTransform = new TranslateTransform();

        _colorSquare.PointerPressed += (sender, e) =>
        {
            e.Pointer.Capture(_colorSquare);
            UpdateColor(e.GetCurrentPoint(_colorSquare).Position);
        };

        _colorSquare.PointerMoved += (sender, e) =>
        {
            if (e.Pointer.Captured == _colorSquare)
                UpdateColor(e.GetCurrentPoint(_colorSquare).Position);
        };

        _hueSlider.PointerPressed += (sender, e) =>
        {
            e.Pointer.Capture(_hueSlider);
            UpdateHue(e.GetCurrentPoint(_hueSlider).Position);
        };

        _hueSlider.PointerMoved += (sender, e) =>
        {
            if (e.Pointer.Captured == _hueSlider)
                UpdateHue(e.GetCurrentPoint(_hueSlider).Position);
        };
    }

    private void UpdateHue(Point position)
    {
        _hue = Clamp(position.Y / _tickHeight, 0, 360);
        UpdateHueThumb();
        UpdateColorProperty();
     }

    private double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, 0), 360);

    private void UpdateHueThumb()
    {
        var y = _hue * _tickHeight;
        if (!double.IsNaN(y))
            ((TranslateTransform)_hueThumb.RenderTransform).Y = y;
    }

    private void UpdateColor(Point point)
    {
        var w = SquareSize.Width;
        var h = SquareSize.Height;
        var x = Math.Min(w, Math.Max(0, point.X));
        var y = Math.Min(h, Math.Max(0, point.Y));

        _sat = x / w;
        _val = 1 - y / h;

        UpdateThumb();

        UpdateColorProperty();
    }

    private void UpdateThumb()
    {
        var x = _sat * SquareSize.Width;
        var y = (1 - _val) * SquareSize.Height;

        if (!double.IsNaN(x))
            ((TranslateTransform)_colorThumb.RenderTransform).X = x;

        if (!double.IsNaN(y))
            ((TranslateTransform)_colorThumb.RenderTransform).Y = y;

        //this.HueSlider.Hue = _hue;
    }

    private void UpdateColorProperty()
    {
        _isPrivateUpdate = true;

        var col = SKColor.FromHsv((float)_hue, (float)_sat * 100, (float)_val * 100);

        if (Color != col)
            Color = col;

        UpdateHueColor();
        _isPrivateUpdate = false;
    }

    private void UpdateHueColor()
    {
        var colHue = SKColor.FromHsv((float)_hue, 100, 100);
        if (_colHue == colHue)
            return;
        ColorHue = colHue.ToColor();
        _colHue = colHue;
    }

    private void UpdateColorOutside(SKColor newValue)
    {
        if (_isPrivateUpdate)
            return;

        newValue.ToHsv(out var h, out var s, out var v);

        s = (float)Math.Round(s);
        v = (float)Math.Round(v);

        _sat = s / 100;
        _val = v / 100;
        _hue = h;

        UpdateThumb();
        UpdateHueThumb();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        Color.ToHsv(out var h, out var s, out var v);

        s = (float)Math.Round(s);
        v = (float)Math.Round(v);

        _sat = s / 100;
        _val = v / 100;
        _hue = h;

        UpdateHueColor();
        UpdateHueThumb();
        UpdateThumb();
        
        return result;
    }
}