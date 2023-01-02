using System;

namespace SkiaNodes.TreeObserver
{
    public class StructureChangedEventArgs : EventArgs
    {
        public SKNode ParentNode { get; protected set; }

    }
}