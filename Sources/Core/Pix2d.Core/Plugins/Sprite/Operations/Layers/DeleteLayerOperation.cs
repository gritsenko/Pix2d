#nullable enable
using Pix2d.Abstract.Operations;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations;

public class DeleteLayerOperation : DeleteNodesOperation, ISpriteEditorOperation
{
    private Pix2dSprite _parent = null!;
    private Pix2dSprite.Layer _deletedLayer = null!;
    private int _oldIndex;
    private int _newIndex;

    public HashSet<int> AffectedLayerIndexes { get; } = [];
    public HashSet<int> AffectedFrameIndexes { get; } = [];

     public DeleteLayerOperation(IEnumerable<SKNode> nodes) : base(nodes)
     {
         _deletedLayer = nodes.FirstOrDefault() as Pix2dSprite.Layer ?? throw new InvalidOperationException("Node is not a Layer");
         _oldIndex = _deletedLayer!.Index;
         _newIndex = Math.Max(0, _deletedLayer.Index - 1);
         _parent = _deletedLayer.Parent as Pix2dSprite ?? throw new InvalidOperationException("Parent is not a Sprite");
     }

    public override void OnPerform()
    {
        base.OnPerform();

        _parent.SelectLayer((_parent.Nodes[_newIndex] as Pix2dSprite.Layer)!);
    }

    public override void OnPerformUndo()
    {
        base.OnPerformUndo();
        _parent.SelectLayer(_deletedLayer);
    }
}
