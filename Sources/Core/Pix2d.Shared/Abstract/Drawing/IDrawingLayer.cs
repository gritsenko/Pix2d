﻿using SkiaNodes;
using SkiaSharp;
using System;
using Pix2d.Primitives.Drawing;

namespace Pix2d.Abstract.Drawing;

/// <summary>
/// UGly stuff that allow draw over pix2d sprites
/// </summary>
public interface IDrawingLayer
{

    event EventHandler DrawingStarted;
    event EventHandler SelectionStarted;
    event EventHandler SelectionRemoved;

    event EventHandler<DrawingAppliedEventArgs> DrawingApplied;
        
    event EventHandler PixelsSelected;
    event EventHandler<PixelsBeforeSelectedEventArgs> PixelsBeforeSelected;
    event EventHandler<SelectionTransformedEventArgs> SelectionTransformed;
    event EventHandler LayerModified;

    bool IsPixelPerfectMode{ get; set; }
    bool UseSwapBitmap { get; set; }
        
    void SetTarget(IDrawingTarget target);

    void DrawWithBitmap(SKBitmap bitmap, SKRect destRect, SKBlendMode compositionMode, float opacity);
    void ClearTarget();

    SKColor DrawingColor { get; set; }
    IPixelBrush Brush { get; set; }

    void SetDrawingLayerMode(BrushDrawingMode drawingMode);

    PixelSelectionMode SelectionMode { get; set; }
    bool HasSelection { get; }
    bool HasSelectionChanges { get; }
    bool MirrorX { get; set; }
    bool MirrorY { get; set; }
    SKPointI GetMirroredPoint(SKPointI p, SKPointI brushOffset = default, int brushSize = default);
    bool ShowBrushPreview { get; set; }
    SKSize SelectionSize { get; }
    IDrawingTarget DrawingTarget { get; }

    SKNode GetSelectionLayer();

    void BeginDrawing();

    /// <summary>
    /// Finalizes current drawing operation and sets the drawing layer as ready to be drawn to the UI.
    /// </summary>
    void FinishCurrentDrawing();
    
    /// <summary>
    /// Applies pixels from working bitmap to target layer, then clears working bitmap
    /// </summary>
    /// <param name="cancel">If true, just clears working bitmap without aplying pixels</param>
    void FinishDrawing(bool cancel = false);

    void ApplyDrawing();

    void ApplySelection(bool saveToUndo = false);
    void InvalidateSelectionEditor();
    void DeactivateSelectionEditor();

    void SetSelectionFromExternal(SKBitmap bitmap, in SKPoint position);
    void SelectAll();
    void FillSelection(SKColor color);
    void ActivateEditor();
    void SetCustomPixelSelector(IPixelSelector pixelSelector);
    void ClearCustomPixelSelector();
    void CancelCurrentOperation();
}