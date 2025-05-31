using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Operations;
using SkiaNodes;

namespace Pix2d.Operations;

public class DeleteNodesOperation : EditOperationBase
{
    private readonly IEnumerable<SKNode> _nodes;
    private NodeStructureState[] _deletedNodes;
    private SKNode[] _parentsOfDeletedNodes;

    public override bool AffectsNodeStructure => true;

    public DeleteNodesOperation(IEnumerable<SKNode> nodes)
    {
            _nodes = nodes;
        }

    public override void OnPerform()
    {
            var parents = GetParentGroups(_nodes).ToArray();

            _deletedNodes = GetNodesToDelete(_nodes);

            foreach (var node in _deletedNodes)
            {
                node.Parent.Nodes.Remove(node.Node);

                //if (node.Node is SymbolNode symbol)
                //{
                //    DeleteSymbol(symbol);
                //}
            }

            //SelectionService.ClearSelection();

            foreach (var groupNode in parents)
            {
                groupNode.UpdateBoundsToContent();
            }

            _parentsOfDeletedNodes = parents.OfType<SKNode>().ToArray();
        }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
            return _deletedNodes.Select(x => x.Node).ToArray();
        }

    private NodeStructureState[] GetNodesToDelete(IEnumerable<SKNode> nodes)
    {
            var nodesToDelete = nodes.ToList();

            AddEmptyGroupsToDelete(ref nodesToDelete);

            //AddSymbolInstancesToDelete(ref nodesToDelete);

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

    //private void AddSymbolInstancesToDelete(ref List<SKNode> nodes)
    //{
    //    foreach (var symbol in nodes.OfType<SymbolNode>().ToArray())
    //    {
    //        var instances = SymbolService.GetSymbolInstances(symbol);
    //        nodes.AddRange(instances);
    //    }
    //}

    private IEnumerable<IGroupNode> GetParentGroups(IEnumerable<SKNode> nodes)
        => nodes.Select(selectedNode => selectedNode.Parent).OfType<IGroupNode>().Distinct();

    public override void OnPerformUndo()
    {
            foreach (var deletedNode in _deletedNodes.OrderBy(n => n.NestingLevel).ThenBy(n => n.Index))
            {
                deletedNode.Parent.Nodes.Insert(deletedNode.Index, deletedNode.Node);

                //if (deletedNode.Node is SymbolNode symbol)
                //{
                //    RestoreSymbol(symbol);
                //}

            }

            //RestoreSymbolInstances();

            //SelectionService.SelectedNodes = _nodes.ToArray();
        }


    //sy,bols delete processing

    //private struct ConvertedSymbolInstance
    //{
    //    public GroupNode Group;
    //    public SKNodeInstance Instance;
    //    public SKNode Parent;
    //    public int Index;

    //    public ConvertedSymbolInstance(SKNodeInstance instance, SKNode parent, int index, GroupNode group)
    //    {
    //        Group = @group;
    //        Instance = instance;
    //        Parent = parent;
    //        Index = index;
    //    }
    //}
    //private List<ConvertedSymbolInstance> _convertedInstances = new List<ConvertedSymbolInstance>();

    //private void DeleteSymbol(SymbolNode symbol)
    //{

    //    var symbolInstances = SymbolService.GetSymbolInstances(symbol).ToArray();

    //    foreach (var instance in symbolInstances)
    //    {
    //        var group = instance.ConvertToGroup();
    //        var record = new ConvertedSymbolInstance(instance, instance.Parent, instance.Index, group);
    //        _convertedInstances.Add(record);

    //        instance.Parent.Nodes.Add(group);

    //    }

    //    SymbolService.UnregisterSymbol(symbol);
    //}

    //private void RestoreSymbol(SymbolNode symbol)
    //{
    //    SymbolService.RegisterSymbol(symbol);
    //}

    //private void RestoreSymbolInstances()
    //{
    //    foreach (var convertedInstance in _convertedInstances)
    //    {
    //        convertedInstance.Parent.Nodes.Remove(convertedInstance.Group);
    //        SymbolService.RegisterSymbolInstance(convertedInstance.Instance);
    //    }
    //}

}