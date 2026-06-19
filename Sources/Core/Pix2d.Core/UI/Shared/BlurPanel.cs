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
        AvaloniaProperty.Register<BlurPanel, IBrush>(nameof(BorderBrush), StaticResources.Brushes.PanelStrokeBrush);

    // Exposed so responsive styles can flatten the corners (e.g. flush top/bottom bars in
    // compact/Narrow mode). Defaults to the rounded panel look so existing callers are unchanged.
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<BlurPanel, CornerRadius>(nameof(CornerRadius), new CornerRadius(StaticResources.Measures.PanelCornerRadius));

    public static readonly StyledProperty<Thickness> BorderThicknessProperty =
        AvaloniaProperty.Register<BlurPanel, Thickness>(nameof(BorderThickness), new Thickness(1));

    public static readonly DirectProperty<BlurPanel, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<BlurPanel, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = null!;

    static BlurPanel()
    {
        AffectsRender<BlurPanel>(BackgroundBrushProperty, BorderBrushProperty, CornerRadiusProperty, BorderThicknessProperty);
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

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public Control Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    protected override object Build() =>
        new Border().Name("BlurPanelBorder")
            .Background(this, x => x.BackgroundBrush, BindingMode.OneWay)
            .CornerRadius(this, x => x.CornerRadius, BindingMode.OneWay)
            .BorderBrush(this, x => x.BorderBrush, BindingMode.OneWay)
            .BorderThickness(this, x => x.BorderThickness, BindingMode.OneWay)
            .Child(this, x => x.Content, BindingMode.OneWay);

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
                GetBrushColor(BorderBrush, StaticResources.Colors.PanelsBorderColor),
                BorderThickness));
        }
    }

    private static SKColor GetBrushColor(IBrush brush, Color fallbackColor)
    {
        if (brush is ISolidColorBrush solidColorBrush)
            return Pix2d.Common.Extensions.ColorExtensions.ToSKColor(solidColorBrush.Color);

        return Pix2d.Common.Extensions.ColorExtensions.ToSKColor(fallbackColor);
    }

}

public class BlurBehindRenderOperation(Rect bounds, float cornerRadius, SKColor backgroundColor, SKColor borderColor, Thickness borderThickness) : ICustomDrawOperation
{
    private readonly Rect _bounds = bounds;
    private readonly float _cornerRadius = cornerRadius;
    private readonly SKColor _backgroundColor = backgroundColor.WithAlpha(127);
    private readonly SKColor _borderColor = borderColor;
    private readonly Thickness _borderThickness = borderThickness;

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

        if (_cornerRadius > 0)
        {
            canvas.DrawRoundRect(rect, _cornerRadius, _cornerRadius, borderPaint);
        }
        else
        {
            if (_borderThickness.Top > 0)
                canvas.DrawLine(0, 0.5f, w, 0.5f, borderPaint);
            if (_borderThickness.Bottom > 0)
                canvas.DrawLine(0, h - 0.5f, w, h - 0.5f, borderPaint);
            if (_borderThickness.Left > 0)
                canvas.DrawLine(0.5f, 0, 0.5f, h, borderPaint);
            if (_borderThickness.Right > 0)
                canvas.DrawLine(w - 0.5f, 0, w - 0.5f, h, borderPaint);
        }
    }

    public Rect Bounds => _bounds.Inflate(4);
    public bool Equals(ICustomDrawOperation? other)
    {
        return other is BlurBehindRenderOperation op
               && op._bounds == _bounds
               && op._cornerRadius.Equals(_cornerRadius)
               && op._backgroundColor == _backgroundColor
               && op._borderColor == _borderColor
               && op._borderThickness == _borderThickness;
    }
}