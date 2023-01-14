using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Operations
{
    public class MoveOperation : TransformOperation
    {
        public MoveOperation(IEnumerable<SKNode> nodes) : base(nodes)
        {
        }
    }
}