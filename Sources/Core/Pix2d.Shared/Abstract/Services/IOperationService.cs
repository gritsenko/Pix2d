using System;
using Pix2d.Primitives.Operations;

namespace Pix2d.Abstract.Operations
{
    public interface IOperationService
    {

        bool CanRedo { get; }
        bool CanUndo { get; }
        int UndoOperationsCount { get; }

        //event EventHandler<OperationInvokeEventArgs> OperationInvoked;

        /// <summary>
        /// Pushes operation (or several of them) into operations stack
        /// if passed more then one operation, they will be merged into one and will be executed as one
        ///
        /// if operation already persist in stack - it won't be pushed
        /// </summary>
        /// <param name="operations">Edit operation(s)</param>
        void PushOperation(params IEditOperation[] operations);

        void Undo();
        void Redo();
        void Clear();


    }
}