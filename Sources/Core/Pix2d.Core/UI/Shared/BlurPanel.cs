using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Shared;

public class BlurPanel : ViewBase
{
    public static readonly StyledProperty<bool> DisableBlurProperty =
        AvaloniaProperty.Register<BlurPanel, bool>(nameof(DisableBlur), true);

    public static readonly StyledProperty<IBrush> BackgroundBrushProperty =
        AvaloniaProperty.Register<BlurPanel, IBrush>(nameof(BackgroundBrush), StaticResources.Brushes.PanelsBackgroundBrush);

    public static readonly StyledProperty<IBrush> BorderBrushProperty =
        AvaloniaProperty.Register<BlurPanel, IBrush>(nameof(BorderBrush), StaticResources.Brushes.PanelsBorderBrush);

    public static readonly DirectProperty<BlurPanel, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<BlurPanel, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = null!;

    static BlurPanel()
    {
        AffectsRender<BlurPanel>(BackgroundBrushProperty, BorderBrushProperty);
    }

    public bool DisableBlur
    {
        get => GetValue(DisableBlurProperty);
        set => SetValue(DisableBlurProperty, value);
    }
    public IBrush BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    public IBrush BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Control Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    protected override object Build() =>
        new Border().Name("BlurPanelBorder")
            .Background(BackgroundBrushProperty)
            .CornerRadius(StaticResources.Measures.PanelCornerRadius)
            .BorderBrush(BorderBrushProperty)
            .Child(ContentProperty);

    public override void Render(DrawingContext context)
    {
        if (DisableBlur)
        {
            base.Render(context);
        }
        else
        {
            context.Custom(new BlurBehindRenderOperation(
                Bounds,
                (float)StaticResources.Measures.PanelCornerRadius,
                GetBrushColor(BackgroundBrush, StaticResources.Colors.PanelsBackgroundColor),
                GetBrushColor(BorderBrush, StaticResources.Colors.PanelsBorderColor)));
        }
    }

    private static SKColor GetBrushColor(IBrush brush, Color fallbackColor)
    {
        if (brush is ISolidColorBrush solidColorBrush)
            return solidColorBrush.Color.ToSKColor();

        return fallbackColor.ToSKColor();
    }

}

public class BlurBehindRenderOperation(Rect bounds, float cornerRadius, SKColor backgroundColor, SKColor borderColor) : ICustomDrawOperation
{
    private readonly Rect _bounds = bounds;
    private readonly float _cornerRadius = cornerRadius;
    private readonly SKColor _backgroundColor = backgroundColor.WithAlpha(127);
    private readonly SKColor _borderColor = borderColor;

    private static readonly SKImageFilter BlurFilter = SKImageFilter.CreateBlur(30, 30, SKShaderTileMode.Clamp);

    public void Dispose()
    {
    }

    public bool HitTest(Point p) => _bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) return;
        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        var w = (float)_bounds.Width;
        var h = (float)_bounds.Height;
        var rect = new SKRect(0, 0, w, h);
        var roundRect = new SKRoundRect(rect, _cornerRadius, _cornerRadius);

        // 1. Ограничиваем область, чтобы Skia не пыталась размыть вообще всё за пределами панели
        canvas.Save();
        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, true);

        // 2. Создаем фильтр размытия
        using var blurFilter = SKImageFilter.CreateBlur(3f, 3f, SKShaderTileMode.Clamp);

        // 3. Настраиваем структуру SaveLayerRec
        var rec = new SKCanvasSaveLayerRec
        {
            Bounds = rect,
            Backdrop = blurFilter // Указываем, что хотим размыть фон под слоем!
        };

        // 4. Магия Skia: создаем слой с фильтрацией фона
        canvas.SaveLayer(in rec);

        // 5. Заливаем полупрозрачным цветом (Tint) ПОВЕРХ размытого фона
        using var tintPaint = new SKPaint { Color = _backgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawPaint(tintPaint); // DrawPaint зальет только область клиппинга

        // 6. Применяем слой к основному холсту
        canvas.Restore();

        // Снимаем клиппинг
        canvas.Restore();

        // 7. Рисуем бордер поверх всего
        using var borderPaint = new SKPaint
        {
            Color = _borderColor,
            IsStroke = true,
            StrokeWidth = 1f,
            IsAntialias = true
        };
        canvas.DrawRoundRect(rect, _cornerRadius, _cornerRadius, borderPaint);
    }

    public Rect Bounds => _bounds.Inflate(4);
    public bool Equals(ICustomDrawOperation? other)
    {
        return other is BlurBehindRenderOperation op
               && op._bounds == _bounds
               && op._cornerRadius.Equals(_cornerRadius)
               && op._backgroundColor == _backgroundColor
               && op._borderColor == _borderColor;
    }
}