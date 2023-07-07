using System;
using Pix2d.Abstract.Drawing;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Abstract.Services
{
    public interface IDrawingService
    {
        #region move to messengeer

        event EventHandler Drawn;
        event EventHandler DrawingTargetChanged;

        #endregion

        #region move to state

        // SKColor CurrentColor { get; set; }
        IDrawingLayer DrawingLayer { get; }

        // bool HasSelection { get; }

        // float BrushScale { get; set; }
        // float BrushOpacity { get; set; }
        // List<BrushSettings> BrushPresets { get; set; }
        // BrushSettings BrushSettings { get; set; }
        // bool IsPixelPerfectModeEnabled { get; set; }

        #endregion

        void SetCurrentColor(SKColor value);
        void SetDrawingTarget(IDrawingTarget targetNode);
        void UpdateDrawingTarget();
        SKColor PickColorByPoint(SKPoint worldPos);
        void InitBrushSettings();

        void ClearCurrentLayer();
        void SetMirrorMode(MirrorMode mode, bool enable);
        void PasteBitmap(SKBitmap bitmap, SKPoint pos);
        void ChangeBrushSize(float delta);

        IPixelSelectionEditor GetSelectionEditor();
        void SelectAll();
    }
}