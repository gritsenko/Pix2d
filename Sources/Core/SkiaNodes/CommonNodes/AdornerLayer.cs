using SkiaNodes.Abstract;

namespace SkiaNodes;

public class AdornerLayer : SKNode
{
    private static IViewPortProvider _viewPortProvider;

    public static void Initialize(IViewPortProvider viewPortProvider)
    {
        _viewPortProvider = viewPortProvider;
    }

    public override bool IsAdorner => true;

    private ViewPort _viewPort;
    private readonly HashSet<IViewPortBindable> _viewPortBindables = [];

    public SKNode TargetNode { get; private set; }

    public ViewPort ViewPort
    {
        get => _viewPort;
        set
        {
            _viewPort = value;
            if (_viewPort != null)
            {
                _viewPort.ViewChanged += ViewPort_ViewChanged;
            }
        }
    }

    private void ViewPort_ViewChanged(object sender, System.EventArgs e)
    {
        foreach (var node in _viewPortBindables)
            node.OnViewChanged(_viewPort);
    }

    public static AdornerLayer GetAdornerLayer(SKNode node)
    {
        if (_viewPortProvider == null)
            throw new InvalidOperationException("AdornerLayer is not initialized. Call AdornerLayer.Initialize() first");

        var adornerLayer = node.AdornerLayer;

        if (adornerLayer == null)
        {
            adornerLayer = new AdornerLayer()
            {
                TargetNode = node,
                ViewPort = _viewPortProvider.ViewPort
            };
            node.AdornerLayer = adornerLayer;
        }
        adornerLayer.Position = node.GetGlobalPosition();

        return adornerLayer;
    }

    protected override void OnChildrenAdded(IEnumerable<SKNode> newNodes)
    {
        AddViewPortBindables(newNodes);
    }

    private void AddViewPortBindables(IEnumerable<SKNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is IViewPortBindable vpb)
            {
                _viewPortBindables.Add(vpb);

                if (ViewPort != null)
                {
                    vpb.OnViewChanged(ViewPort);
                }
            }

            AddViewPortBindables(node.Nodes);
        }
    }

    protected override void OnChildrenRemoved(IEnumerable<SKNode> removedNodes)
    {
        RemoveViewPortBindables(removedNodes);
    }

    private void RemoveViewPortBindables(IEnumerable<SKNode> nodes)
    {
        foreach (var node in nodes)
        {
            RemoveViewPortBindables(node.Nodes);

            if (node is IViewPortBindable vpb && _viewPortBindables.Contains(vpb))
                _viewPortBindables.Remove(vpb);
        }
    }


    public void Add(SKNode node)
    {
        Nodes.Add(node);
    }
}