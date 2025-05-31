using System.Collections.Generic;

namespace SkiaNodes.TreeObserver;

public class NodesRemovedEventArgs : StructureChangedEventArgs
{
    public IEnumerable<SKNode> RemovedNodes { get; }

    public NodesRemovedEventArgs(SKNode removedFrom, IEnumerable<SKNode> removedNodes)
    {
            ParentNode = removedFrom;
            RemovedNodes = removedNodes;
        }
}