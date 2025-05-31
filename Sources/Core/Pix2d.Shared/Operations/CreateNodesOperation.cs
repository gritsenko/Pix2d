using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Operations;

public class CreateNodesOperation : EditOperationBase
{
    private readonly IEnumerable<SKNode> _nodes;
    private NodeStructureState[] _createdNodes;
    private SKNode[] _parentsOfCreatedNodes;

    public override bool AffectsNodeStructure => true;

    public CreateNodesOperation(IEnumerable<SKNode> nodes)
    {
        _nodes = nodes;
    }

    public override void OnPerform()
    {
        foreach (var createdNode in _createdNodes.OrderBy(n => n.NestingLevel).ThenBy(n => n.Index))
        {
            createdNode.Parent.Nodes.Insert(createdNode.Index, createdNode.Node);
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _createdNodes.Select(x => x.Node).ToArray();
    }

    private NodeStructureState[] GetNodesToDelete(IEnumerable<SKNode> nodes)
    {
        var nodesToDelete = nodes.ToList();

        AddEmptyGroupsToDelete(ref nodesToDelete);

        return nodesToDelete
            .Distinct()
            .Select(x => new NodeStructureState(x))
            .ToArray();
    }

    private void AddEmptyGroupsToDelete(ref List<SKNode> nodes)
    {
        foreach (var node in nodes.ToArray())
        {
            var parentNode = node.Parent;

            while (parentNode is IGroupNode && parentNode.Nodes.All(nodes.Contains))
            {
                var nodeToDelete = parentNode;
                parentNode = parentNode.Parent;
                nodes.Add(nodeToDelete);
            }
        }
    }

    private IEnumerable<IGroupNode> GetParentGroups(IEnumerable<SKNode> nodes)
        => nodes.Select(selectedNode => selectedNode.Parent).OfType<IGroupNode>().Distinct();

    public override void OnPerformUndo()
    {

        var parents = GetParentGroups(_nodes).ToArray();

        _createdNodes = GetNodesToDelete(_nodes);

        foreach (var node in _createdNodes)
        {
            node.Parent.Nodes.Remove(node.Node);
        }

        foreach (var groupNode in parents)
        {
            groupNode.UpdateBoundsToContent();
        }

        _parentsOfCreatedNodes = parents.OfType<SKNode>().ToArray();
    }
}