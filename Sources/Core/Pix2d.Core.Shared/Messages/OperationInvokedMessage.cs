using Pix2d.Abstract.Operations;

namespace Pix2d.Messages
{
    public class OperationInvokedMessage
    {
        public OperationEventType OperationType;
        public IEditOperation Operation;

        public OperationInvokedMessage(OperationEventType operationType, IEditOperation operation)
        {
            OperationType = operationType;
            Operation = operation;
        }
    }
}