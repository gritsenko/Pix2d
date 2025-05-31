using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Operations;

public class ResizeOperation : TransformOperation
{
    public ResizeOperation(IEnumerable<SKNode> nodes) : base(nodes)
    {
        }
}