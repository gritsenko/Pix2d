using System.Diagnostics;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Shared;

public class BlurPanel : ViewBase
{
    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<BlurPanel, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<BlurPanel, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = default;

    public Control Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    protected override object Build() =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .CornerRadius(StaticResources.Measures.PanelCornerRadius)
            .BorderBrush(StaticResources.Brushes.PanelsBorderBrush)
            .Child(ContentProperty);

    //public override void Render(DrawingContext context)
    //{
    //    //base.Render(context);
    //    context.Custom(new BlurBehindRenderOperation(Bounds, (float)StaticResources.Measures.PanelCornerRadius));
    //}

}

public class BlurBehindRenderOperation(Rect bounds, float cornerRadius) : ICustomDrawOperation
{
    private readonly Rect _bounds = bounds;
    private readonly float _cornerRadius = cornerRadius;

    private static readonly SKPaint BgPaint = new() { Color = StaticResources.Colors.PanelsBackgroundColor.ToSKColor() };
    private static readonly SKPaint BorderPaint = new() { Color = StaticResources.Colors.PanelsBorderColor.ToSKColor(), IsStroke = true, StrokeWidth = 1f, IsAntialias = true };
    private static readonly SKImageFilter BlurFilter = SKImageFilter.CreateBlur(30, 30, SKShaderTileMode.Clamp);

    public void Dispose()
    {
    }

    public bool HitTest(Point p) => _bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null)
            return;
        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        var w = (float)_bounds.Width;
        var h = (float)_bounds.Height;

        if (false)
        {
            var scale = canvas.TotalMatrix.ScaleX;

            using var backgroundSnapshot =
                lease.SkSurface.Snapshot(
                    new SKRectI(
                        (int)Math.Ceiling(canvas.TotalMatrix.TransX),
                        (int)Math.Ceiling(canvas.TotalMatrix.TransY),
                        (int)Math.Floor(canvas.TotalMatrix.TransX + _bounds.Width * scale),
                        (int)Math.Floor(canvas.TotalMatrix.TransY + _bounds.Height * scale))
                );

            using var backdropShader = SKShader.CreateImage(backgroundSnapshot, SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp, SKMatrix.CreateScale(1 / scale, 1 / scale));

            using var blurred = SKSurface.Create(lease.GrContext, false, new SKImageInfo(
                (int)Math.Ceiling(w),
                (int)Math.Ceiling(h), SKImageInfo.PlatformColorType, SKAlphaType.Premul));

            using var blurPaint = new SKPaint { Shader = backdropShader, ImageFilter = BlurFilter };
            blurred.Canvas.DrawRect(0, 0, w, h, blurPaint);

            using var blurSnap = blurred.Snapshot();
            using var blurSnapShader = SKShader.CreateImage(blurSnap);
            using var blurSnapPaint = new SKPaint { Shader = blurSnapShader };
            canvas.DrawRoundRect(0, 0, w, h, _cornerRadius, _cornerRadius, blurSnapPaint);

        }
        canvas.DrawRoundRect(0, 0, w, h, _cornerRadius, _cornerRadius, BgPaint);
        canvas.DrawRoundRect(0, 0, w, h, _cornerRadius, _cornerRadius, BorderPaint);
    }

    public Rect Bounds => _bounds.Inflate(4);
    public bool Equals(ICustomDrawOperation? other)
    {
        return other is BlurBehindRenderOperation op && op._bounds == _bounds && op._cornerRadius.Equals(_cornerRadius);
    }
}