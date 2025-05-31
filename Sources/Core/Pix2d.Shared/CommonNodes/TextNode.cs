using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class TextNode : SKNode
{
    private string _text;
    private SKRect _bounds;
    private string _fontFamily = "Arial";
    private float _fontSize = 12;
    private bool _bold;
    private bool _italic;
    private bool _aliased;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            UpdateTextBounds();
        }
    }

    public float FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            UpdateTextBounds();
        }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            UpdateTextBounds();
        }
    }

    public bool Bold
    {
        get => _bold;
        set
        {
            _bold = value;
            UpdateTextBounds();
        }
    }

    public bool Italic
    {
        get => _italic;
        set
        {
            _italic = value;
            UpdateTextBounds();
        }
    }

    public bool Aliased
    {
        get => _aliased;
        set
        {
            _aliased = value;
            UpdateTextBounds();
        }
    }


    public SKColor Color { get; set; } = SKColors.Black;

    public void ResizeToText()
    {
        UpdateTextBounds();

        Size = _bounds.Size;
    }

    private void UpdateTextBounds()
    {
        var paint = GetPaint();
        _bounds = SKRect.Empty;
        paint.MeasureText(Text, ref _bounds);
        var height = -paint.FontMetrics.Top + paint.FontMetrics.Bottom;
        // This adds same margin to the right of the text as to the left of the text.
        // Without this margin sometimes one pixel is lost at the right because of rounding.
        var right = _bounds.Right + _bounds.Left;
        _bounds.Top = 0;
        _bounds.Bottom = height;
        _bounds.Right = right;
    }

    private SKPaint GetPaint()
    {
        var paint = new SKPaint();
        paint.TextSize = FontSize;
        var style = new SKFontStyle(
            weight: Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            width: SKFontStyleWidth.Normal,
            slant: Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        paint.TextAlign = SKTextAlign.Left;

        paint.SubpixelText = !Aliased;
        paint.IsAntialias = !Aliased;

        paint.Typeface = SKTypeface.FromFamilyName(FontFamily, style);
        paint.Color = Color;
        return paint;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;

        var paint = GetPaint();
        canvas.DrawText(Text, new SKPoint(-_bounds.Left, _bounds.Size.Height - paint.FontMetrics.Bottom), paint);
    }
}