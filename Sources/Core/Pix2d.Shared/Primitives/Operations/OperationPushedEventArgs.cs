using System;
using Pix2d.Abstract.Operations;

namespace Pix2d.Primitives.Operations
{
    public class OperationPushedEventArgs : EventArgs
    {
        public readonly IEditOperation Operation;

        public OperationPushedEventArgs(IEditOperation operation)
        {
            Operation = operation;
        }
    }
}