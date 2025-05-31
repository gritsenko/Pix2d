using SkiaNodes;

namespace Pix2d.Operations.Effects;

public class AddEffectOperation(IEnumerable<SKNode> nodes, ISKNodeEffect effect) : EditOperationBase
{
    private readonly SKNode[] _nodes = nodes.ToArray();
    private ISKNodeEffect[] _addedEffects;

    public override void OnPerform()
    {
        if (_addedEffects == null)
        {
            _addedEffects = new ISKNodeEffect[_nodes.Length];
            for (var i = 0; i < _nodes.Length; i++)
            {
                _addedEffects[i] = effect;
            }
        }

        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            node.Effects.Add(_addedEffects[i]);
        }
    }

    public override void OnPerformUndo()
    {
        if (_addedEffects != null)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes[i];
                node.Effects.Remove(_addedEffects[i]);
            }
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _nodes;
    }
}