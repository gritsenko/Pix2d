using System;
using Pix2d.Abstract.Operations;

namespace Pix2d.Primitives.Operations;

public class OperationInvokeEventArgs : EventArgs
{
    public OperationEventType OperationType;
    public IEditOperation Operation;

    public OperationInvokeEventArgs(OperationEventType operationType, IEditOperation operation)
    {
            OperationType = operationType;
            Operation = operation;
        }
}