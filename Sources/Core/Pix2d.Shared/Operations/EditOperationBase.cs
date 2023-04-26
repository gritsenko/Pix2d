using System.Collections.Generic;
using CommonServiceLocator;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using SkiaNodes;

namespace Pix2d.Operations
{
    public abstract class EditOperationBase : IEditOperation
    {
        public virtual bool AffectsNodeStructure { get; protected set; }

        protected IOperationService OperationService => ServiceLocator.Current.GetInstance<IOperationService>();

        public void Invoke()
        {
            OnPerform();
            OperationService.PushOperation(this);
        }

        public void PushToHistory()
        {
            OperationService.PushOperation(this);
        }

        public abstract void OnPerform();
        public abstract void OnPerformUndo();

        public abstract IEnumerable<SKNode> GetEditedNodes();
    }
}
