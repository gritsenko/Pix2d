using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations
{
    public class MergeLayerOperation : DeleteNodesOperation
    {
        private Pix2dSprite _parent;
        private Pix2dSprite.Layer _mergedLayer;
        private int _oldIndex;
        private int _newIndex;
        private byte[][] _mergeTargetLayerDatas;
        private Pix2dSprite.Layer _targetLayer;

        public MergeLayerOperation(IEnumerable<SKNode> nodes) : base(nodes)
        {
            _mergedLayer = nodes.FirstOrDefault() as Pix2dSprite.Layer;
            _oldIndex = _mergedLayer.Index;
            _newIndex = Math.Max(0, _mergedLayer.Index - 1);
            _parent = _mergedLayer.Parent as Pix2dSprite;

            _targetLayer = _parent.Nodes[_newIndex] as Pix2dSprite.Layer;
            _mergeTargetLayerDatas = _targetLayer.Nodes.OfType<BitmapNode>().Select(x => x.GetData()).ToArray();
        }

        public override void OnPerform()
        {
            _parent.MergeDownLayer(_mergedLayer, false);

            base.OnPerform();

            _parent.SelectLayer(_parent.Nodes[_newIndex] as Pix2dSprite.Layer);

        }

        public override void OnPerformUndo()
        {
            base.OnPerformUndo();

            for (var i = 0; i < _mergeTargetLayerDatas.Length; i++)
            {
                var mergeTargetLayerData = _mergeTargetLayerDatas[i];
                var frame = _targetLayer.Nodes[i] as BitmapNode;
                frame.SetData(mergeTargetLayerData);
            }

            _parent.SelectLayer(_mergedLayer);
        }
    }
}