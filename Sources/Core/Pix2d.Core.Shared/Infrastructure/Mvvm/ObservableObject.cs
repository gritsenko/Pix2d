using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Mvvm
{
    public class ObservableObject : INotifyPropertyChanged
    {
        private readonly Dictionary<string, object> _propertyBackingDictionary = new Dictionary<string, object>();

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly SynchronizationContext _notificationContext;


        private ILookup<string, string> _dependentLookup;
        private ILookup<string, string> DependentLookup
        {
            get
            {
                return _dependentLookup ?? (_dependentLookup = (from p in GetType().GetRuntimeProperties()
                           let attrs = p.GetCustomAttributes(typeof(NotifiesOnAttribute), false)
                           from NotifiesOnAttribute a in attrs
                           select new { Independent = a.Name, Dependent = p.Name }).ToLookup(i => i.Independent, d => d.Dependent));
            }
        }

        private ILookup<string, IRelayCommand> _dependentCommandsLookup;
        private bool _disableNotification;

        private ILookup<string, IRelayCommand> DependentCommandsLookup
        {
            get
            {
                return _dependentCommandsLookup ?? (_dependentCommandsLookup =
                           (from p in GetType().GetRuntimeProperties()
                               let attrs = p.GetCustomAttributes(typeof(UpdateCanExecuteAttribute), false)
                               from UpdateCanExecuteAttribute a in attrs
                               select new {Independent = a.Name, Command = p.GetValue(this) as IRelayCommand})
                           .Where(x=>x.Command != null)
                           .ToLookup(i => i.Independent, d => d.Command));
            }
        }

        public ObservableObject()
        {
            _notificationContext = SynchronizationContext.Current;
        }

        protected void RunInUiThread(Action action)
        {
            if (_notificationContext != null)
                _notificationContext.Post(_ => action(), null);
            else
                action?.Invoke();
        }

        protected T Get<T>(T defaultValue = default(T), [CallerMemberName] string propertyName = null)
        {
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

            object value;

            if (_propertyBackingDictionary.TryGetValue(propertyName, out value))
            {
                return (T)value;
            }

            _propertyBackingDictionary[propertyName] = defaultValue;
            return defaultValue;
        }

        protected void Set<T>(T newValue, Action<T> onValueChanged, [CallerMemberName] string propertyName = null)
        {
            if (Set(newValue, false, propertyName))
                onValueChanged?.Invoke(newValue);
        }

        protected bool Set<T>(T newValue, bool forceNotifyPropertyChanged = false, [CallerMemberName] string propertyName = null)
        {
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

            if (!EqualityComparer<T>.Default.Equals(newValue, Get(default(T), propertyName))
                || forceNotifyPropertyChanged)
            {
                _propertyBackingDictionary[propertyName] = newValue;

                if (_disableNotification) return false;

                OnPropertyChanged(propertyName);
                return true;
            }

            return false;
        }

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));

            var propertyChanged = PropertyChanged;

            if (propertyChanged != null)
            {
                RunInUiThread(() =>
                {
                    propertyChanged(this, new PropertyChangedEventArgs(propertyName));

                    foreach (var dependentCommand in DependentCommandsLookup[propertyName])
                    {
                        dependentCommand.RaiseCanExecuteChanged();
                    }

                    foreach (var dependentPropertyName in DependentLookup[propertyName])
                    {
                        OnPropertyChanged(dependentPropertyName);
                    }
                });
            }
        }

        protected void InvokeWithoutOnPropertyChanged(Action action)
        {
            
            _disableNotification = true;
            action?.Invoke();
            _disableNotification = false;
        }


        protected bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            // We duplicate the code here instead of calling the overload because we can't
            // guarantee that the invoked SetProperty<T> will be inlined, and we need the JIT
            // to be able to see the full EqualityComparer<T>.Default.Equals call, so that
            // it'll use the intrinsics version of it and just replace the whole invocation
            // with a direct comparison when possible (eg. for primitive numeric types).
            // This is the fastest SetProperty<T> overload so we particularly care about
            // the codegen quality here, and the code is small and simple enough so that
            // duplicating it still doesn't make the whole class harder to maintain.
            if (EqualityComparer<T>.Default.Equals(field, newValue))
            {
                return false;
            }

            field = newValue;

            OnPropertyChangedLite(propertyName);

            return true;
        }

        private void OnPropertyChangedLite([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}