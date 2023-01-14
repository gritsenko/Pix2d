using System;
using System.Reflection;

namespace Mvvm
{
    public class WeakAction<T> : WeakAction, IExecuteWithObject
    {
        private Action<T> _staticAction;

        public override string MethodName => _staticAction != null ? _staticAction.GetMethodInfo().Name : Method.Name;

        public override bool IsAlive
        {
            get
            {
                if (_staticAction == null
                    && Reference == null)
                {
                    return false;
                }

                if (_staticAction == null) return Reference.IsAlive;
                return Reference == null || Reference.IsAlive;
            }
        }

        public WeakAction(Action<T> action)
            : this(action?.Target, action)
        {
        }

        public WeakAction(object target, Action<T> action)
        {
            if (action.GetMethodInfo().IsStatic)
            {
                _staticAction = action;

                if (target != null)
                    Reference = new WeakReference(target);

                return;
            }

            Method = action.GetMethodInfo();
            ActionReference = new WeakReference(action.Target);
            Reference = new WeakReference(target);
        }

        public new void Execute()
        {
            Execute(default(T));
        }

        public void Execute(T parameter)
        {
            if (_staticAction != null)
            {
                _staticAction(parameter);
                return;
            }

            var actionTarget = ActionTarget;

            if (!IsAlive) return;
            if (Method != null
                && ActionReference != null
                && actionTarget != null)
            {
                Method.Invoke(
                    actionTarget,
                    new object[]
                    {
                        parameter
                    });
            }
        }

        public void ExecuteWithObject(object parameter)
        {
            var parameterCasted = (T) parameter;
            Execute(parameterCasted);
        }

        public new void MarkForDeletion()
        {
            _staticAction = null;
            base.MarkForDeletion();
        }
    }
}