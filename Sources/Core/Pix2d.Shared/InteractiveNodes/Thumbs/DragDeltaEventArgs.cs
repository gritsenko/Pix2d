namespace Pix2d.InteractiveNodes.Thumbs;

public class DragDeltaEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the DragDeltaEventArgs class.</summary>
    /// <param name="horizontalChange">The horizontal change in the Thumb position since the last DragDelta event.</param>
    /// <param name="verticalChange">The vertical change in the Thumb position since the last DragDelta event.</param>
    public DragDeltaEventArgs(float horizontalChange, float verticalChange)
    {
            HorizontalChange = horizontalChange;
            VerticalChange = verticalChange;
        }

    /// <summary>Gets the horizontal change in the Thumb position since the last DragDelta event.</summary>
    /// <returns>The horizontal change in the Thumb position since the last DragDelta event.</returns>
    public float HorizontalChange;

    /// <summary>Gets the vertical change in the Thumb position since the last DragDelta event.</summary>
    /// <returns>The vertical change in the Thumb position since the last DragDelta event.</returns>
    public float VerticalChange;

}