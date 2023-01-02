using System;

namespace Pix2d.CommonNodes.Controls.Thumbs
{
    public class DragCompletedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the DragCompletedEventArgs class.</summary>
        /// <param name="horizontalChange">The horizontal change in position of the Thumb control, resulting from the drag operation.</param>
        /// <param name="verticalChange">The vertical change in position of the Thumb control, resulting from the drag operation.</param>
        /// <param name="canceled">A value that indicates whether the drag operation was canceled by a call to the CancelDrag method.</param>
        public DragCompletedEventArgs(float horizontalChange, float verticalChange, bool canceled)
        {
            HorizontalChange = horizontalChange;
            VerticalChange = verticalChange;
            Canceled = canceled;
        }

        /// <summary>Gets the horizontal distance between the current mouse position and the thumb coordinates.</summary>
        /// <returns>The horizontal distance between the current mouse position and the thumb coordinates.</returns>
        public float HorizontalChange;

        /// <summary>Gets the vertical distance between the current mouse position and the thumb coordinates.</summary>
        /// <returns>The vertical distance between the current mouse position and the thumb coordinates.</returns>
        public float VerticalChange;

        /// <summary>Gets a value that indicates whether the drag operation was canceled.</summary>
        /// <returns>**true** if the drag operation was canceled; otherwise, **false**.</returns>
        public bool Canceled;

    }
}