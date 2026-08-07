using Pix2d.Abstract.Drawing;
using Pix2d.Primitives.Drawing;
using Pix2d.Services;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Nodes;

internal interface IPointerInputRouterHost
{
    BrushDrawingMode GetDrawingMode();
    DrawingLayerState State { get; }
    PixelSelectionMode SelectionMode { get; }
    IAspectSnapper? AspectSnapper { get; }
    bool HasSelection { get; }
    bool IsTargetBitmapVisible { get; }
    SKColor DrawingColor { get; }
    float FillOpacity { get; }
    SKPoint StartPos { get; set; }
    SKPoint EndPos { get; }
    SKPointI StartPosI { get; }
    SKPointI EndPosI { get; }
    bool IsPointerOverSelection(SKPoint worldPos);
    void ApplySelection();
    void Refresh();
    void CapturePointer();
    void BeginSelection(SKPoint pos, SelectionCombineMode combineMode);
    void AddSelectionPoint(SKPoint p);
    void SetSelectionRect(SKPoint startPos, SKPoint endPos);
    void BeginDrawing();
    void DrawStroke(SKPoint pos);
    void FinishSelection();
    void CancelMarquee();
    void FillRegion(SKPoint origin, SKColor fillColor, float tolerance = 0, SKBlendMode blendMode = SKBlendMode.SrcOver);
    void FinishReleasedDrawing();
}