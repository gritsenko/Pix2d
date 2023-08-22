using System;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;

namespace Pix2d.Plugins.Sprite.Operations
{
    public abstract class EditSpriteOperationBase : TransformOperation
    {
        public class FrameNodeData
        {
            public byte[] Data;
        }
        public class LayerData
        {
            public FrameNodeData[] FrameNodes;
        }

        protected readonly Pix2dSprite _targetSprite;
        protected LayerData[] _unmodifidSpriteData;

        public EditSpriteOperationBase(Pix2dSprite targetSprite)
        {
            SetInitialData(targetSprite.GetDescendants(null, false, true));
            _targetSprite = targetSprite;
            _unmodifidSpriteData = GetFramesData(_targetSprite);
        }
        protected LayerData[] GetFramesData(Pix2dSprite targetSprite)
        {
            var result = new LayerData[targetSprite.Layers.Count()];
            
            foreach (var targetSpriteLayer in targetSprite.Layers)
            {
                var frames = new FrameNodeData[targetSpriteLayer.Nodes.Count];
                foreach (var frameNode in targetSpriteLayer.Nodes.OfType<BitmapNode>())
                {
                    frames[frameNode.Index] = new FrameNodeData
                    {
                        Data = frameNode.GetData()
                    };
                }

                result[targetSpriteLayer.Index] = new LayerData {FrameNodes = frames};
            }

            return result;
        }

        protected void SetFramesData(Pix2dSprite targetSprite, LayerData[] data)
        {
            foreach (var targetSpriteLayer in targetSprite.Layers)
            {
                foreach (var frameNode in targetSpriteLayer.Nodes.OfType<BitmapNode>())
                {
                    var pixelsData = data[targetSpriteLayer.Index].FrameNodes[frameNode.Index];
                    frameNode.SetData(pixelsData.Data);
                }
            }
        }
    }

    public class EditSpriteOperation : EditSpriteOperationBase
    {
        private LayerData[] _finalData;

        public EditSpriteOperation(Pix2dSprite targetSprite) : base(targetSprite)
        {
        }

        public Action Callback { get; set; }

        public new void SetFinalData()
        {
            _finalData = GetFramesData(_targetSprite);
            base.SetFinalData();
        }

        public override void OnPerform()
        {
            base.OnPerform();
            SetFramesData(_targetSprite, _finalData);
            Callback?.Invoke();
        }

        public override void OnPerformUndo()
        {
            base.OnPerformUndo();
            SetFramesData(_targetSprite, _unmodifidSpriteData);
            Callback?.Invoke();
        }
    }
}