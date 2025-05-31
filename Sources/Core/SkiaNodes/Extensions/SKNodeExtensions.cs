using SkiaNodes.Render;
using SkiaNodes.Serialization;
using SkiaSharp;
using System.Diagnostics;

namespace SkiaNodes.Extensions;

public static class SKNodeExtensions
{
    public static SKRect GetChildrenBounds(this SKNode node)
    {
        return GetBounds(node.Nodes.ToArray());
    }

    public static SKRect GetBounds(this IEnumerable<SKNode> nodes)
    {
        return (nodes as SKNode[] ?? nodes.ToArray()).GetBounds();
    }

    public static SKRect GetBounds(this SKNode[] nodes)
    {
        if (nodes.Length == 0)
            return new SKRect();
        var rect = nodes[0].GetBoundingBox();
        for (var i = 1; i < nodes.Length; i++)
            rect.Union(nodes[i].GetBoundingBox());

        return rect;
    }

    public static SKNode GetTopNode(this IEnumerable<SKNode> nodes)
    {
        return nodes?.LastOrDefault();
    }

    public static SKNode GetBottomNode(this IEnumerable<SKNode> nodes)
    {
        return nodes?.FirstOrDefault();
    }

    public static SKNode GetTopNode(this IEnumerable<SKNode> nodes, Func<SKNode, bool> condition)
    {
        return nodes?.LastOrDefault(condition);
    }

    public static SKNode GetBottomNode(this IEnumerable<SKNode> nodes, Func<SKNode, bool> condition)
    {
        return nodes?.FirstOrDefault(condition);
    }

    public static IEnumerable<SKNode> GetParents(this IEnumerable<SKNode> nodes)
    {
        return nodes.Select(selectedNode => selectedNode.Parent).Distinct();
    }

    public static IEnumerable<SKNode> GetParentsChain(this SKNode node)
    {
        var chain = new List<SKNode>();
        var n = node.Parent;
        while (n != null)
        {
            chain.Insert(0, n);
            n = n.Parent;
        }

        return chain;
    }

    public static bool IsInLockedBranch(this SKNode node)
    {
        if (node.DesignerState.IsLocked)
            return true;
        return node.GetParentsChain().Any(x => x.DesignerState.IsLocked);
    }

    /// <summary>
    /// Traverse through node tree and return all visible descendants of current node
    /// </summary>
    /// <param name="node">Node to process</param>
    /// <param name="includeSelf">include this node in result enumeration</param>
    /// <returns></returns>
    public static IEnumerable<SKNode> GetDescendants(this SKNode node, bool includeSelf = false)
    {
        var toProcess = new Stack<SKNode>([node]);

        while (toProcess.Any())
        {
            var curNode = toProcess.Pop();

            if (includeSelf || node != curNode)
                yield return curNode;

            for (var i = curNode.Nodes.Count - 1; i >= 0; i--)
                toProcess.Push(curNode.Nodes[i]);
        }
    }

    public static SKNode FindChildByName(this SKNode parent, string childName)
    {
        return parent.GetVisibleDescendants(x => x.Name == childName).FirstOrDefault();
    }

    /// <summary>
    /// Traverse through node tree and return all visible descendands of current node
    /// </summary>
    /// <param name="filterCondition">return only nodes if condition returns True</param>
    /// <param name="includeAdorners">add adorner layer and it's nodes to result</param>
    /// <param name="includeSelf">include this node in result enumeration</param>
    /// <returns></returns>
    public static IEnumerable<SKNode> GetVisibleDescendants(this SKNode node, Func<SKNode, bool> filterCondition = null,
        bool includeAdorners = true, bool includeSelf = false)
    {
        var toProcess = new Stack<SKNode>();

        void PushNode(SKNode n)
        {
            if (includeAdorners && n.HasAdornerLayer)
                toProcess.Push(n.AdornerLayer);

            toProcess.Push(n);
        }

        PushNode(node);

        while (toProcess.Any())
        {
            var curNode = toProcess.Pop();

            if (!curNode.IsVisible)
                continue;

            if ((includeSelf || node != curNode) && (filterCondition == null || filterCondition(curNode)))
                yield return curNode;

            for (var i = curNode.Nodes.Count - 1; i >= 0; i--)
            {
                var curChild = curNode.Nodes[i];
                if (curChild == null)
                {
                    // This means that the node structure was changed in race condition and we probably want to skip
                    // this frame. Otherwise something terrible might happen.
#if DEBUG
                    Debugger.Break();
#else
                        yield break;
#endif
                }

                PushNode(curChild);
            }
        }
    }

    /// <summary>
    /// Clones node using Node Serializer
    /// </summary>
    /// <param name="node">Node to clone</param>
    /// <returns></returns>
    public static TNode Clone<TNode>(this TNode node) where TNode : SKNode
    {
        using var serializer = new NodeSerializer();
        var nodeDef = serializer.Serialize(node);

        var result = NodeSerializer.Deserialize(typeof(TNode), nodeDef, serializer.GetDataEntries());
        return result as TNode;
    }

    public static void AlignToLeft(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var minx = narr.GetBounds().Left;

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(minx, node.GetGlobalPosition().Y));
    }

    public static void AlignToRight(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var minx = narr.GetBounds().Right;

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(minx - node.GetBoundingBox().Width, node.GetGlobalPosition().Y));
    }

    public static void AlignToCenterHorizontally(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var bounds = narr.GetBounds();

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(bounds.Left + (bounds.Width - node.GetBoundingBox().Width) / 2,
                node.GetGlobalPosition().Y));
    }

    public static void AlignToTop(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var miny = narr.GetBounds().Top;

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(node.GetGlobalPosition().X, miny));
    }

    public static void AlignToCenterVertically(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var bounds = narr.GetBounds();

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(node.GetGlobalPosition().X,
                bounds.Top + (bounds.Height - node.GetBoundingBox().Height) / 2));
    }

    public static void AlignToBottom(this IEnumerable<SKNode> nodes)
    {
        var narr = nodes.ToArray();
        var maxy = narr.GetBounds().Bottom;

        foreach (var node in narr)
            node.SetGlobalPosition(new SKPoint(node.GetGlobalPosition().X, maxy - node.GetBoundingBox().Height));
    }

    public static SKBitmap RenderToBitmap(this IEnumerable<SKNode> nodes, SKColor fillColor = default, double scale = 1)
    {
        var bounds = nodes.GetBounds();
        var w = (int)(bounds.Width * scale);
        var h = (int)(bounds.Height * scale);

        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKApp.ColorType, SKAlphaType.Premul));
        var vp = new ViewPort(w, h) { Settings = { RenderAdorners = false } };
        vp.ShowArea(bounds);
        var canvas = new SKCanvas(bitmap);

        // check for transparency for fill color
        if (fillColor == default)
            canvas.Clear();
        else
            canvas.Clear(fillColor);

        var ctx = new RenderContext(canvas, vp);
        foreach (var node in nodes)
            SKNodeRenderer.Render(node, ctx);

        return bitmap;
    }
}