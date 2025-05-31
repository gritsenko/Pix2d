using System.Linq;
using SkiaNodes.Extensions;

namespace SkiaNodes;

public class GroupNode : SKNode, IGroupNode
{
    public void UpdateBoundsToContent()
    {
            var rect = Nodes.ToArray().GetBounds();

            //var bbox = GetBoundingBoxWithContent();
            this.Size = rect.Size;

            var oldPos = this.Position;
            this.Position = Parent.GetLocalPosition(rect.Location);

            foreach(var node in Nodes)
            {
                var delta = rect.Location - oldPos;
                node.Position = node.Position - delta;
            }
        }
}