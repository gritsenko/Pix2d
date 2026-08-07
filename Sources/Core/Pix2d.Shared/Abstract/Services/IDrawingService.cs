using Pix2d.Abstract.Drawing;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Abstract.Services;

/// <summary>
/// Defines a service for managing drawing operations and state within the application.
/// </summary>
public interface IDrawingService
{
    #region move to state

    /// <summary>
    /// Gets the drawing layer used by this service for rendering and interactions.
    /// </summary>
    IDrawingLayer DrawingLayer { get; }

    #endregion

    /// <summary>
    /// Sets the current color used for drawing operations.
    /// </summary>
    /// <param name="value">The SKColor to set as the current drawing color.</param>
    void SetCurrentColor(SKColor value);

    /// <summary>
    /// Sets the target node where drawing operations will be applied.
    /// </summary>
    /// <param name="targetNode">The IDrawingTarget node.</param>
    void SetDrawingTarget(IDrawingTarget targetNode);

    /// <summary>
    /// Updates the current drawing target, typically based on the application's current state
    /// like the selected layer or sprite.
    /// </summary>
    void UpdateDrawingTarget();

    /// <summary>
    /// Picks a color from the current drawing target at a specified world position.
    /// </summary>
    /// <param name="worldPos">The world coordinates (SKPoint) to sample the color from.</param>
    /// <returns>The SKColor at the specified position, or SKColor.Empty if no target is set or the position is out of bounds.</returns>
    SKColor PickColorByPoint(SKPoint worldPos);

    /// <summary>
    /// Initializes or resets the available brush settings and presets.
    /// </summary>
    void InitBrushSettings();

    /// <summary>
    /// Adds the current brush settings to the preset row as a user preset and persists it. Returns the stored
    /// preset, or the existing one when an identical preset is already present (saving never duplicates), or
    /// null when the current brush has no stable key to store it under.
    /// </summary>
    BrushSettings? SaveCurrentBrushAsPreset();

    /// <summary>
    /// Removes a preset from the row and persists the change. A user preset is dropped for good; a built-in
    /// preset is only hidden (its stable id is remembered) so <see cref="ResetBrushPresetsToDefaults"/> can
    /// bring it back later.
    /// </summary>
    /// <returns>True when the preset was removed.</returns>
    bool DeleteBrushPreset(BrushSettings preset);

    /// <summary>
    /// Captures the current pixel selection as a new preset and appends it to the row, persisting the change.
    /// <paramref name="useOriginalColors"/> true reproduces the selection's own colors (a decal); false treats
    /// it as a recolorable shape mask, like every other brush. Returns null when there is no active selection.
    /// </summary>
    BrushSettings? CreateBrushPresetFromSelection(bool useOriginalColors);

    /// <summary>
    /// Restores every built-in preset the user has removed via <see cref="DeleteBrushPreset"/>. Presets the
    /// user actually saved (plain or captured from a selection) are left untouched.
    /// </summary>
    void ResetBrushPresetsToDefaults();

    /// <summary>
    /// Clears the entire content of the current drawing layer.
    /// </summary>
    void ClearCurrentLayer();

    /// <summary>
    /// Enables or disables a specific mirror mode for drawing operations.
    /// </summary>
    /// <param name="mode">The MirrorMode to set (Horizontal, Vertical, or Both).</param>
    /// <param name="enable">A boolean value indicating whether to enable (true) or disable (false) the mirror mode.</param>
    void SetMirrorMode(MirrorMode mode, bool enable);

    /// <summary>
    /// Pastes a given bitmap onto the current drawing target at a specified position.
    /// </summary>
    /// <param name="bitmap">The SKBitmap to paste.</param>
    /// <param name="pos">The position (SKPoint) on the drawing target where the bitmap should be pasted.</param>
    void PasteBitmap(SKBitmap bitmap, SKPoint pos);

    /// <summary>
    /// Changes the size of the currently active brush by a given delta value.
    /// </summary>
    /// <param name="delta">The amount (float) to add to the current brush size. Can be positive or negative.</param>
    void ChangeBrushSize(float delta);

    /// <summary>
    /// Gets the pixel selection editor associated with the current drawing layer,
    /// allowing manipulation of pixel selections.
    /// </summary>
    /// <returns>An IPixelSelectionEditor instance.</returns>
    IPixelSelectionEditor GetSelectionEditor();

    /// <summary>
    /// Selects all pixels on the current drawing layer.
    /// </summary>
    void SelectAll();

    /// <summary>
    /// Selects every non-transparent pixel of <paramref name="maskSource"/> — the layer-thumbnail
    /// Ctrl+click gesture ("load layer transparency as a selection"). The mask commonly comes from a
    /// layer other than the one being edited; it is ignored unless it matches the drawing target's size.
    /// </summary>
    void SelectOpaquePixels(SKBitmap? maskSource);

    /// <summary>
    /// Inverts the current pixel selection on the active drawing layer.
    /// If no selection exists, selects the whole layer.
    /// </summary>
    void InvertSelection();

    /// <summary>
    /// Splits the current ongoing drawing operation, effectively starting a new operation
    /// without finalizing the previous one immediately. Useful for undo/redo granularity.
    /// </summary>
    void SplitCurrentOperation();

    /// <summary>
    /// Cancels the current ongoing drawing operation without applying any changes.
    /// </summary>
    void CancelCurrentOperation();

    /// <summary>
    /// Cancels only an in-progress drawing or selection-area drag. Keeps an already-applied selection and a
    /// pending paste intact. Use this when aborting transient state for touch gestures (pinch, pan) where
    /// removing the user's selection would be surprising.
    /// </summary>
    void CancelActiveDrawing();

    /// <summary>
    /// Commits the currently-transforming pixel selection onto its drawing target AND records the commit as a
    /// single undo step. Only valid when <see cref="IDrawingLayer.SelectionPhase"/> is <c>Transforming</c>;
    /// returns silently otherwise. The recorded operation is tool-aware so undo/redo can restore the transform
    /// tool that produced the commit. Pass <paramref name="keepMarqueeInContour"/>=true when handing off to a
    /// selection tool that should keep the marquee alive in contour mode; false to drop the marquee outright.
    /// </summary>
    void CommitTransformWithUndo(bool keepMarqueeInContour, string? toolKeyBefore, string? toolKeyAfter);
}