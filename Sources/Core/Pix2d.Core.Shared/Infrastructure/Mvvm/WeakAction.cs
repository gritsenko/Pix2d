using System;
using System.Reflection;

namespace Mvvm
{
    public class WeakAction
    {
        private Action _staticAction;

        protected MethodInfo Method { get; set; }

        public virtual string MethodName => _staticAction != null ? _staticAction.GetMethodInfo().Name : Method.Name;

        protected WeakReference ActionReference { get; set; }

        protected WeakReference Reference { get; set; }

        public bool IsStatic => _staticAction != null;

        protected WeakAction()
        {
        }

        public WeakAction(Action action) : this(action?.Target, action)
        {
        }

        public WeakAction(object target, Action action)
        {
            if (action.GetMethodInfo().IsStatic)
            {
                _staticAction = action;

                if (target != null)
                {
                    // Keep a reference to the target to control the
                    // WeakAction's lifetime.
                    Reference = new WeakReference(target);
                }

                return;
            }

            Method = action.GetMethodInfo();
            ActionReference = new WeakReference(action.Target);
            Reference = new WeakReference(target);
        }

        public virtual bool IsAlive
        {
            get
            {
                if (_staticAction == null && Reference == null)
                    return false;

                if (_staticAction == null) return Reference.IsAlive;
                return Reference == null || Reference.IsAlive;
            }
        }

        public object Target => Reference?.Target;

        protected object ActionTarget => ActionReference?.Target;

        public void Execute()
        {
            if (_staticAction != null)
            {
                _staticAction();
                return;
            }

            var actionTarget = ActionTarget;

            if (!IsAlive) return;
            if (Method == null || ActionReference == null || actionTarget == null) return;
            Method.Invoke(actionTarget, null);
        }

        public void MarkForDeletion()
        {
            Reference = null;
            ActionReference = null;
            Method = null;
            _staticAction = null;
        }
    }
}