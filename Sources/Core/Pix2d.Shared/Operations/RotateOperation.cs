﻿using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Operations;

public class RotateOperation: TransformOperation
{
    public RotateOperation(IEnumerable<SKNode> nodes) : base(nodes)
    {
        }
}