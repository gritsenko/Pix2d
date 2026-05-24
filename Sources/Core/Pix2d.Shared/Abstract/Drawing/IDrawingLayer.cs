using SkiaNodes;
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

    /// <summary>
    /// Fired only when a user just finished creating a fresh marquee (Rect/Lasso/Color drag, Select-All) —
    /// distinct from <see cref="PixelsSelected"/> which also fires on undo/redo replay. Use this to push
    /// a <c>BeginSelectionOperation</c> exactly once per user-initiated marquee.
    /// </summary>
    event EventHandler MarqueeFinishedByUser;

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

    /// <summary>
    /// Lifecycle of the current selection marquee. None / MarqueeReady (contour-only) / Transforming (pixels lifted).
    /// </summary>
    SelectionPhase SelectionPhase { get; }
    bool MirrorX { get; set; }
    bool MirrorY { get; set; }
    SKPointI GetMirroredPoint(SKPointI p, SKPointI brushOffset = default, int brushSize = default);
    bool ShowBrushPreview { get; set; }
    SKSize SelectionSize { get; }
    IDrawingTarget? DrawingTarget { get; }

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

    /// <summary>
    /// Activates the selection editor in an explicit mode. <c>contourOnly: true</c> keeps pixels in place and
    /// just shows the marching-ants outline; <c>contourOnly: false</c> lifts pixels onto the selection layer
    /// and exposes resize/rotate handles. Called by the transform tool when the user wants to manipulate
    /// the selected pixels.
    /// </summary>
    void ActivateEditor(bool contourOnly);

    /// <summary>
    /// Switches the live selection editor between full transform mode (resize/rotate handles, blue circles)
    /// and contour-edit mode (marching-ants outline + simple dark move/resize thumbs). No-op when there
    /// is no active selection. Called by the transform tool's deactivation hand-off; the canonical user-facing
    /// path into transform mode is to activate <c>PixelTransformTool</c>, which owns the Transforming phase.
    /// </summary>
    void SetSelectionTransformMode(bool transformMode);

    /// <summary>
    /// Crop-tool frame-resize mode. While enabled, the selection editor keeps the resize handles visible
    /// in contour styling (black) on top of contour mode, so the user can resize the marquee region
    /// without lifting pixels — used by the crop tool to present its frame. Persists across fresh
    /// marquees drawn while the mode is active; reset when the crop tool deactivates.
    /// </summary>
    void SetFrameResizeMode(bool enabled);
    void SetCustomPixelSelector(IPixelSelector pixelSelector);
    void ClearCustomPixelSelector();
    void CancelCurrentOperation();

    /// <summary>
    /// Cancels an in-progress drawing or selection-area drag. Unlike <see cref="CancelCurrentOperation"/>,
    /// this does not remove an already-applied selection or a pending paste.
    /// </summary>
    void CancelActiveDrawing();
}