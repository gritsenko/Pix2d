using SkiaNodes;

namespace Pix2d.Operations;

public abstract class ChangeNodePropertyOperationBase<TValue> : EditOperationBase
{
    private SKNode[] _nodes;
    private TValue[] _initialValues;
    private TValue[] _finalValues;

    protected ChangeNodePropertyOperationBase(IEnumerable<SKNode> nodes)
    {
        SetInitialData(nodes);
    }

    protected abstract TValue GetValue(SKNode node);
    protected abstract void SetValue(SKNode node, TValue value);

    public void SetInitialData(IEnumerable<SKNode> nodes)
    {
        _nodes = nodes.ToArray();
        _initialValues = _nodes.Select(GetValue).ToArray();
    }
    public virtual void SetFinalData()
    {
        _finalValues = _nodes.Select(GetValue).ToArray();
    }

    public override void OnPerform()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            SetValue(node, _finalValues[i]);
        }
    }

    public override void OnPerformUndo()
    {
        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            SetValue(node, _initialValues[i]);
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _nodes;
    }

}