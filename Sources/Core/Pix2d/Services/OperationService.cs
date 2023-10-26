using System.Diagnostics;
using System.Linq;
using Pix2d.Abstract.Operations;
using Pix2d.Messages;
using Pix2d.Operations;
using Pix2d.Primitives;
using Pix2d.Primitives.Operations;

namespace Pix2d.Services
{
    public class OperationService : IOperationService
    {
        public const int FreeUndoSteps = 30;
        public const int ProUndoSteps = 100;
        
        private readonly LimitedSizeStack<IEditOperation> _redoOperations = new(FreeUndoSteps);

        private readonly LimitedSizeStack<IEditOperation> _undoOperations = new(ProUndoSteps) { OnRemoveItem = OnRemoveItemFromHistory };

        private IEditOperation _currentOperation;

        public bool CanUndo => _undoOperations.Any();
        public int UndoOperationsCount => _undoOperations.Count;
        public bool CanRedo => _redoOperations.Any();

        public event EventHandler<OperationInvokeEventArgs> OperationInvoked;

        public OperationService()
        {
            Messenger.Default.Register<ProjectLoadedMessage>(this, OnProjectLoaded);
            CoreServices.LicenseService.LicenseChanged += OnLicenseChange;
            UpdateMaxUndoLength();
        }

        private void OnLicenseChange(object sender, EventArgs e)
        {
            UpdateMaxUndoLength();
        }

        private void UpdateMaxUndoLength()
        {
            var maxLength = GetMaxUndoLength();
            if (_undoOperations.MaxSize != maxLength)
            {
                _undoOperations.ChangeMaxSize(maxLength);
                _redoOperations.ChangeMaxSize(maxLength);
            }
        }

        private int GetMaxUndoLength()
        {
            switch (CoreServices.LicenseService.License)
            {
                case LicenseType.Pro:
                case LicenseType.Ultimate:
                    return ProUndoSteps;
                default:
                    return FreeUndoSteps;
            }
                
        }

        private void OnProjectLoaded(ProjectLoadedMessage message)
        {
            Clear();
        }

        public void PushOperation(params IEditOperation[] operations)
        {
            var ops = operations.Where(x => x != null).ToArray();

            if (ops.Length == 0 || IsAlreadyPushed(ops))
                return;

            var operation = ops.Length > 1 ? new BulkEditOperation(operations) : operations[0];

            _undoOperations.Push(operation);

            ClearRedoOperations();

#if DEBUG
            System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
            var path = "\n" + string.Join(" \\ ", t.GetFrames().Take(3).Select(x => x.GetMethod().DeclaringType.Name + "." + x.GetMethod().Name).Reverse());
            Debug.WriteLine("Operation ("+_undoOperations.Count+") pushed: " + operation.GetType() + " from:" +path);
#endif

            OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Perform, operation));
        }

        private void ClearRedoOperations()
        {
            foreach (var operation in _redoOperations.OfType<IDisposable>()) operation.Dispose();
            _redoOperations.Clear();
        }

        //dispose items before remove from history
        private static void OnRemoveItemFromHistory(IEditOperation operation)
        {
            if(operation is IDisposable dop)
                dop.Dispose();
        }

        //todo: optimize in 2020
        internal bool IsAlreadyPushed(IEditOperation[] ops)
        {
            bool EqualOrContains(IEditOperation op1, IEditOperation op2)
            {
                return op1 == op2 || op1 is BulkEditOperation bulkOp && bulkOp.HasOperation(op2);
            }

            foreach (var op in ops)
            {
                if (EqualOrContains(_currentOperation, op))
                {
                    Debug.WriteLine("Operation already pushed: " + op.GetType());
                    return true;
                }

                foreach (var operation in _undoOperations)
                    if (EqualOrContains(operation, op))
                    {
                        Debug.WriteLine("Operation already pushed: " + op.GetType());
                        return true;
                    }

                foreach (var operation in _redoOperations)
                    if (EqualOrContains(operation, op))
                    {
                        Debug.WriteLine("Operation already pushed: " + op.GetType());
                        return true;
                    }
            }


            return false;
        }

        public void Undo()
        {
            if (_undoOperations.Count == 0) return;

            _currentOperation = _undoOperations.Pop();

            if (_currentOperation == null) return;

            _currentOperation.OnPerformUndo();
            _redoOperations.Push(_currentOperation);

            Debug.WriteLine("Operation Undo performed: " + _currentOperation.GetType());

            OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Undo, _currentOperation));
        }

        public void Redo()
        {
            if (_redoOperations.Count == 0) return;

            _currentOperation = _redoOperations.Pop();
            _currentOperation.OnPerform();

            _undoOperations.Push(_currentOperation);

            Debug.WriteLine("Operation Redo performed: " + _currentOperation.GetType());

            OnOperationInvoked(new OperationInvokeEventArgs(OperationEventType.Redo, _currentOperation));
        }

        public void Clear()
        {
            _undoOperations.Clear();
            _redoOperations.Clear();

            GC.Collect();
        }


        protected virtual void OnOperationInvoked(OperationInvokeEventArgs e)
        {
            OperationInvoked?.Invoke(this, e);
            Messenger.Default.Send(new OperationInvokedMessage(e.OperationType, e.Operation));
        }
    }
}