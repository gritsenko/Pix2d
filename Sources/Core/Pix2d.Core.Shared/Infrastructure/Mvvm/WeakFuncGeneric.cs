using System;
using System.Reflection;

namespace Mvvm
{
    public class WeakFunc<T, TResult> : WeakFunc<TResult>
    {
        private Func<T, TResult> _staticFunc;

        public override string MethodName => _staticFunc != null ? _staticFunc.GetMethodInfo().Name : Method.Name;
        public override bool IsAlive
        {
            get
            {
                if (_staticFunc == null
                    && Reference == null)
                {
                    return false;
                }

                if (_staticFunc == null) return Reference.IsAlive;
                return Reference == null || Reference.IsAlive;
            }
        }

        public WeakFunc(Func<T, TResult> func)
            : this(func?.Target, func)
        {
        }

        public WeakFunc(object target, Func<T, TResult> func)
        {
            if (func.GetMethodInfo().IsStatic)
            {
                _staticFunc = func;

                if (target != null)
                    Reference = new WeakReference(target);

                return;
            }

            Method = func.GetMethodInfo();
            FuncReference = new WeakReference(func.Target);
            Reference = new WeakReference(target);
        }

        public new TResult Execute()
        {
            return Execute(default(T));
        }

        public TResult Execute(T parameter)
        {
            if (_staticFunc != null)
            {
                return _staticFunc(parameter);
            }

            var funcTarget = FuncTarget;

            if (!IsAlive) return default(TResult);
            if (Method != null
                && FuncReference != null
                && funcTarget != null)
            {
                return (TResult) Method.Invoke(
                    funcTarget,
                    new object[]
                    {
                        parameter
                    });
            }

            return default(TResult);
        }

        public object ExecuteWithObject(object parameter)
        {
            var parameterCasted = (T)parameter;
            return Execute(parameterCasted);
        }
        public new void MarkForDeletion()
        {
            _staticFunc = null;
            base.MarkForDeletion();
        }
    }
}