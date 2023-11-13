using System;
using System.Collections.Generic;
using System.Diagnostics;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Drawing.Operations
{
    public class DrawingOperation : EditOperationBase, IDisposable
    {
        private readonly IDrawingTarget _drawingTarget;

        private byte[] _initialData;
        private byte[] _finalData;
        private int _frame;
        private int _layerIndex;
        private int _finalFrame;
        private int _finalLayerIndex;

        public DrawingOperation(IDrawingTarget drawingTarget)
        {
            _drawingTarget = drawingTarget;
            _initialData = _drawingTarget.GetData();

            if (_drawingTarget is IAnimatedNode sprite)
            {
                _frame = sprite.CurrentFrameIndex;
                _layerIndex = sprite.SelectedLayerIndex;

                AffectedFrameIndexes = new() {_frame};
            }
        }

        public void SetFinalData()
        {
            _finalData = _drawingTarget.GetData();
            if (_finalData != null && _finalData[0] == 0)
            {
                // Debugger.Break();
            }
            
            if (_drawingTarget is IAnimatedNode sprite)
            {
                _finalFrame = sprite.CurrentFrameIndex;
                _finalLayerIndex = sprite.SelectedLayerIndex;

                if (_finalFrame != _frame)
                {
                    AffectedFrameIndexes.Add(_finalFrame);
                }
            }
        }

        public override void OnPerform()
        {
            if (_drawingTarget is IAnimatedNode sprite)
            {
                sprite.SelectedLayerIndex = _finalLayerIndex;
                sprite.SetFrameIndex(_finalFrame);
            }

            _drawingTarget.SetData(_finalData);
        }

        public override void OnPerformUndo()
        {
            if (_drawingTarget is IAnimatedNode sprite)
            {
                sprite.SelectedLayerIndex = _layerIndex;
                sprite.SetFrameIndex(_frame);
            }

            _drawingTarget.SetData(_initialData);
        }

        public override IEnumerable<SKNode> GetEditedNodes()
        {
            yield return _drawingTarget as SKNode;
        }

        public IDrawingTarget GetDrawingTarget() => _drawingTarget;

        public bool HasChanges()
        {
            if (_finalData == null)
            {
                return _initialData != null;
            }

            if (_initialData == null)
            {
                return true;
            }

            return !((ReadOnlySpan<byte>) _finalData).SequenceEqual((ReadOnlySpan<byte>) _initialData);
        }
        
        public void Dispose()
        {
            _initialData = null;
            _finalData = null;
        }

        public bool CanMerge(DrawingOperation operation)
        {
            return _drawingTarget == operation._drawingTarget && _frame == operation._frame &&
                _layerIndex == operation._layerIndex;
        }

        public void Merge(DrawingOperation operation)
        {
            if (!CanMerge(operation))
            {
                throw new InvalidOperationException("Operation drawing targets are not same");
            }

            _finalData = operation._finalData;
        }
    }
}