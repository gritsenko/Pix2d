using System;

namespace SkiaNodes.Interactive;

public class RootNodeChangedEventArgs : EventArgs
{
    public SKNode OldRootNode;
    public SKNode NewRootNode;

    public RootNodeChangedEventArgs(SKNode oldRootNode, SKNode newRootNode)
    {
            OldRootNode = oldRootNode;
            NewRootNode = newRootNode;
        }
}