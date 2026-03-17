namespace Pix2d.Plugins.Drawing;

public enum DrawingLayerState
{
    /// <summary>
    /// This can be either nothing is happening on the drawing layer, or selection editor is active and showing the
    /// selection layer. This is different from DrawingLayerState.DrawingSelectionArea which is active when the user
    /// draws a rectangle or path to select util they release the mouse button.
    /// </summary>
    Ready = 0,
    Drawing = 1,
    DrawingSelectionArea = 2,
        
    /// <summary>
    /// Selection editor is active, but its contents are from external source like paste operation, image import or
    /// text editing.
    /// </summary>
    Paste = 3
}