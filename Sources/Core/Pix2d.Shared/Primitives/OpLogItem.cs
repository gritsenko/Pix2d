using Pix2d.Abstract.Operations;

namespace Pix2d.Primitives
{
    public class OpLogItem
    {
        public OpLogItem(OperationEventType operationType, string info) : this(operationType)
        {
            Operation += info;
        }
        public OpLogItem(IEditOperation operation, OperationEventType operationType) : this(operationType)
        {
            Operation += operation.GetType().Name;
        }
        public OpLogItem(OperationEventType operationType)
        {
            var opt = "";
            switch (operationType)
            {
                case OperationEventType.Undo:
                    opt = "↶";
                    break;
                case OperationEventType.Redo:
                    opt = "↷";
                    break;
                case OperationEventType.Command:
                    opt = "↵";
                    break;
                case OperationEventType.Event:
                    opt = "ε";
                    break;
                case OperationEventType.Error:
                    opt = "⚠";
                    break;
            }
            Operation = opt;
        }

        public string Operation { get; set; }
        public int Count { get; set; }
        public OperationEventType Direction { get; set; }

        public override string ToString()
        {
            return $"{Operation}:{Count}";
        }
    }
}