using System.Collections.Generic;

namespace SkiaNodes.TreeObserver
{
    public class NodesAddedEventArgs : StructureChangedEventArgs
    {
        public IEnumerable<SKNode> AddedNodes { get; }

        public NodesAddedEventArgs(SKNode addedTo, IEnumerable<SKNode> addedNodes)
        {
            ParentNode = addedTo;
            AddedNodes = addedNodes;
        }
    }
}