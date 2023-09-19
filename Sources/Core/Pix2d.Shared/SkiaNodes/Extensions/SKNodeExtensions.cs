using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d;
using SkiaNodes.Serialization;
using SkiaSharp;

namespace SkiaNodes.Extensions
{
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
            if(nodes.Length == 0)
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


        public static SKNode FindChildByName(this SKNode parent, string childName)
        {
            return parent.GetVisibleDescendants(x => x.Name == childName).FirstOrDefault();
        }

        /// <summary>
        /// Clones node using Node Serializer
        /// </summary>
        /// <param name="node">Node to clone</param>
        /// <returns></returns>
        public static TNode Clone<TNode>(this TNode node) where TNode : SKNode
        {
            using (var serializer = new NodeSerializer())
            {
                var nodeDef = serializer.Serialize(node);

                var result = NodeSerializer.Deserialize(node.GetType(), nodeDef, serializer.GetDataEntries());
                return result as TNode;
            }
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
                node.SetGlobalPosition(new SKPoint(bounds.Left + (bounds.Width - node.GetBoundingBox().Width) / 2, node.GetGlobalPosition().Y));
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
                node.SetGlobalPosition(new SKPoint(node.GetGlobalPosition().X, bounds.Top + (bounds.Height - node.GetBoundingBox().Height) / 2));
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

            var bitmap = new SKBitmap(new SKImageInfo(w, h, Pix2DAppSettings.ColorType, SKAlphaType.Premul));
            var vp = new ViewPort(w, h) {Settings = {RenderAdorners = false}};
            vp.ShowArea(bounds);
            var canvas = new SKCanvas(bitmap);

            // check for transparency for fill color
            if (fillColor == default)
                canvas.Clear();
            else
                canvas.Clear(fillColor);

            foreach (var node in nodes) 
                node.Render(canvas, vp);

            return bitmap;
        }

    }
}