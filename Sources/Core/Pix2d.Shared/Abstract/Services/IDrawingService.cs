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
    /// Splits the current ongoing drawing operation, effectively starting a new operation
    /// without finalizing the previous one immediately. Useful for undo/redo granularity.
    /// </summary>
    void SplitCurrentOperation();

    /// <summary>
    /// Cancels the current ongoing drawing operation without applying any changes.
    /// </summary>
    void CancelCurrentOperation();
}