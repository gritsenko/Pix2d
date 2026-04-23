using SkiaNodes;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class TextNode : SKNode
{
    private const float RightPadding = 1f;
    private string _text = string.Empty;
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
        using var font = GetFont();
        using var paint = GetPaint();
        _bounds = SKRect.Empty;

        if (string.IsNullOrEmpty(Text))
            return;

        paint.Typeface = font.Typeface;
        paint.TextSize = font.Size;

        var bounds = new SKRect();
#pragma warning disable CS0618
        paint.MeasureText(Text, ref bounds);
#pragma warning restore CS0618
        var height = MathF.Ceiling(-font.Metrics.Top + font.Metrics.Bottom);
        var width = MathF.Ceiling(bounds.Width + MathF.Max(bounds.Left, 0) + RightPadding);
        _bounds = new SKRect(bounds.Left, 0, bounds.Left + width, height);
    }

    private SKPaint GetPaint()
    {
        var paint = new SKPaint();
        paint.Color = Color;
        paint.IsAntialias = !Aliased;
        return paint;
    }

    private SKFont GetFont()
    {
        var style = new SKFontStyle(
            weight: Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            width: SKFontStyleWidth.Normal,
            slant: Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        var font = new SKFont(SKTypeface.FromFamilyName(FontFamily, style), FontSize);
        font.Subpixel = !Aliased;
        return font;
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;

        var font = GetFont();
        var paint = GetPaint();
        canvas.DrawText(Text, new SKPoint(-_bounds.Left, _bounds.Size.Height - font.Metrics.Bottom), SKTextAlign.Left, font, paint);
    }
}