using System;

namespace Pix2d.CommonNodes.Controls.Thumbs
{
    public class DragStartedEventArgs : EventArgs
    {
        /// <summary>Initializes a new instance of the DragStartedEventArgs class.</summary>
        /// <param name="horizontalOffset">The horizontal distance between the current mouse position and the thumb coordinates.</param>
        /// <param name="verticalOffset">The vertical distance between the current mouse position and the thumb coordinates.</param>
        public DragStartedEventArgs(float horizontalOffset, float verticalOffset)
        {
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
        }

        /// <summary>Gets the horizontal distance between the current mouse position and the thumb coordinates.</summary>
        /// <returns>The horizontal distance between the current mouse position and the thumb coordinates.</returns>
        public float HorizontalOffset;

        /// <summary>Gets the vertical distance between the current mouse position and the thumb coordinates.</summary>
        /// <returns>The vertical distance between the current mouse position and the thumb coordinates.</returns>
        public float VerticalOffset;

    }
}