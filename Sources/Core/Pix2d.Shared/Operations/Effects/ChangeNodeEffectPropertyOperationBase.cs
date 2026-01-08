using SkiaNodes;

namespace Pix2d.Operations.Effects;

public abstract class ChangeNodeEffectPropertyOperationBase<TValue> : EditOperationBase
{
    private SKNode[]? _nodes;
    private TValue[]? _initialValues;
    private TValue[]? _finalValues;

    protected ChangeNodeEffectPropertyOperationBase(IEnumerable<SKNode> nodes, ISKNodeEffect effect, TValue oldValue, TValue newValue)
    {
        SetInitialData(nodes, effect);
    }

    protected abstract TValue GetValue(SKNode node);
    protected abstract void SetValue(SKNode node, TValue value);

    public void SetInitialData(IEnumerable<SKNode> nodes, ISKNodeEffect effect)
    {
        _nodes = nodes.ToArray();
        _initialValues = _nodes!.Select(GetValue).ToArray();
    }
    public virtual void SetFinalData()
    {
        if (_nodes != null)
        {
            _finalValues = _nodes!.Select(GetValue).ToArray();
        }
    }

    public override void OnPerform()
    {
        if (_nodes != null && _finalValues != null)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes![i];
                SetValue(node, _finalValues![i]);
            }
        }
    }

    public override void OnPerformUndo()
    {
        if (_nodes != null && _initialValues != null)
        {
            for (var i = 0; i < _nodes.Length; i++)
            {
                var node = _nodes![i];
                SetValue(node, _initialValues![i]);
            }
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _nodes ?? Array.Empty<SKNode>();
    }

}