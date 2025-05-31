using Pix2d.Abstract.NodeTypes;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaNodes.Extensions;

namespace Pix2d.Plugins.Sprite.Operations.Effects;

public class BakeEffectOperation : EditOperationBase
{
    private SKNode _node;
    private ISKNodeEffect _effect;

    private readonly List<(IBitmapNode, byte[])> _frameNodesWithOriginalData = new();

    public BakeEffectOperation(SKNode node, ISKNodeEffect effect)
    {
        _effect = effect;
        _node = node;

        SaveLayerFramesData();
    }

    private void SaveLayerFramesData()
    {
        if (!(_node is Pix2dSprite.Layer layer)) return;

        foreach (var layerNode in layer.Nodes.OfType<IBitmapNode>())
            _frameNodesWithOriginalData.Add((layerNode, layerNode.Bitmap.GetPixelSpan().ToArray()));
    }

    public override void OnPerform()
    {
        foreach (var (frameNode, _) in _frameNodesWithOriginalData)
        {
            _effect.ApplyToBitmap(frameNode.Bitmap);
            frameNode.Bitmap.NotifyPixelsChanged();
        }

        _node.Effects.Remove(_effect);
    }

    public override void OnPerformUndo()
    {
        foreach (var (frameNode, data) in _frameNodesWithOriginalData)
        {
            frameNode.Bitmap.CopyPixelsToBitmap(data);
        }

        _node.Effects.Add(_effect);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _node.Yield();
    }
}