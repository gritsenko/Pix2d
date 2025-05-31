using SkiaSharp;

namespace SkiaNodes.Render;

internal sealed class RenderScope : IDisposable
{
    private readonly SKNode _node;
    private readonly RenderContext _context;
    private readonly SKCanvas _canvas;
    private int _layerId = -1;
    private readonly SKMatrix _originalMatrix;
    private SKMatrix _adornerMatrix;
    private SKPaint? _paint;

    public SKMatrix AdornerTransform => _adornerMatrix;

    public RenderScope(SKNode node, RenderContext context)
    {
        _node = node;
        _context = context;
        _canvas = context.Canvas;
        _originalMatrix = _canvas.TotalMatrix;

        ApplyTransforms();
        SetupLayer();
    }

    private void ApplyTransforms()
    {
        // Сохраняем оригинальную матрицу для адорнеров
        _adornerMatrix = _canvas.TotalMatrix;

        // Применяем трансформации ноды
        var transform = _node.Transform;
        _canvas.Concat(ref transform);
    }

    private void SetupLayer()
    {
        var opacity = _context.Opacity * _node.Opacity;
        var needsLayer = opacity < 0.99f ||
                         _node.BlendMode != SKBlendMode.SrcOver ||
                         _node.Effects?.Count > 0;

        if (!needsLayer) return;

        _paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha((byte)(opacity * 255)),
            BlendMode = _node.BlendMode
        };

        // Для клиппинга через родителя
        if (_node.Parent is IClippingSource { ClipMode: SKNodeClipMode.Rect } cs)
        {
            _layerId = _canvas.SaveLayer(cs.ClipBounds, _paint);
            _context.Canvas.ClipRect(cs.ClipBounds);
        }
        else
        {
            _layerId = _canvas.SaveLayer(_paint);
        }
    }

    public RenderContext CreateChildContext()
    {
        //pass effects to children nodes
        //var effects = _context.InheritedEffects.Concat(_node.Effects);

        return new RenderContext(
            _canvas,
            _context.ViewPort,
            _context.Opacity * _node.Opacity //allow to render onion skin frames by apply external opacity
            //effects.ToImmutableList()
        );
    }

    public void Dispose()
    {
        _canvas.SetMatrix(_originalMatrix);

        if (_layerId != -1)
            _canvas.Restore();

        _paint?.Dispose();
    }
}