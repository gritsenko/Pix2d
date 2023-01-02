using System;
using Pix2d.Abstract.Selection;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes.Controls.Thumbs
{
    public class NodeManipulateThumbBase : ThumbNode
    {

        private NodesSelection _selection;

        public bool SnapToPixels { get; set; }

        public NodesSelection TargetSelection
        {
            get => _selection;
            set
            {
                if (_selection != value)
                {

                    if (_selection != null)
                        _selection.Invalidated -= SelectionOnInvalidated;

                    _selection = value;

                    if (_selection != null)
                        _selection.Invalidated += SelectionOnInvalidated;

                    AdjustDimensionsToTargets(_selection);
                }

            }
        }

        protected virtual void AdjustDimensionsToTargets(NodesSelection selection)
        {

            var bounds = selection.Bounds;
            Position = bounds.Location;
            Size = bounds.Size;
        }

        protected void DragNode(SKNode node, SKPoint initialGlobalPos, SKPoint delta, bool snapToPixels)
        {
            var newGlobalPos = initialGlobalPos + delta;
            node.SetGlobalPosition(snapToPixels ? newGlobalPos.Floor() : newGlobalPos);
        }

        private void SelectionOnInvalidated(object sender, EventArgs e)
        {
            AdjustDimensionsToTargets(_selection);
        }

        public void UpdateToTargets()
        {
            AdjustDimensionsToTargets(_selection);
        }
    }
}
