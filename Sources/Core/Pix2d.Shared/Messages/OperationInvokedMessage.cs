using Pix2d.Abstract.Operations;

namespace Pix2d.Messages;

public class OperationInvokedMessage(OperationEventType operationType, IEditOperation operation)
{
    public OperationEventType OperationType = operationType;
    public IEditOperation Operation = operation;
}